﻿using System.Threading.Tasks;
using BrickController2.CreationManagement;
using BrickController2.UI.Services.Navigation;
using System.Windows.Input;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Commands;
using System.Linq;
using BrickController2.DeviceManagement;

namespace BrickController2.UI.ViewModels
{
    public class CreationPageViewModel : PageViewModelBase
    {
        private readonly ICreationManager _creationManager;
        private readonly IDeviceManager _deviceManager;
        private readonly IDialogService _dialogService;

        public CreationPageViewModel(
            INavigationService navigationService,
            ICreationManager creationManager,
            IDeviceManager deviceManager,
            IDialogService dialogService,
            NavigationParameters parameters)
            : base(navigationService)
        {
            _creationManager = creationManager;
            _deviceManager = deviceManager;
            _dialogService = dialogService;

            Creation = parameters.Get<Creation>("creation");

            RenameCreationCommand = new SafeCommand(async () => await RenameCreationAsync());
            DeleteCreationCommand = new SafeCommand(async () => await DeleteCreationAsync());
            PlayCommand = new SafeCommand(async () => await PlayAsync());
            AddControllerProfileCommand = new SafeCommand(async () => await AddControllerProfileAsync());
            ControllerProfileTappedCommand = new SafeCommand<ControllerProfile>(async controllerProfile => await NavigationService.NavigateToAsync<ControllerProfilePageViewModel>(new NavigationParameters(("controllerprofile", controllerProfile))));
        }

        public Creation Creation { get; }

        public ICommand RenameCreationCommand { get; }
        public ICommand DeleteCreationCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand AddControllerProfileCommand { get; }
        public ICommand ControllerProfileTappedCommand { get; }

        private async Task RenameCreationAsync()
        {
            var result = await _dialogService.ShowInputDialogAsync("Rename", "Enter a new creation name", Creation.Name, "Creation name", "Rename", "Cancel");
            if (result.IsOk)
            {
                if (string.IsNullOrWhiteSpace(result.Result))
                {
                    await DisplayAlertAsync("Warning", "Creation name can not be empty.", "Ok");
                    return;
                }

                await _dialogService.ShowProgressDialogAsync(
                    false,
                    async (progressDialog, token) => await _creationManager.RenameCreationAsync(Creation, result.Result),
                    "Renaming...");
            }
        }

        private async Task DeleteCreationAsync()
        {
            if (await _dialogService.ShowQuestionDialogAsync("Confirm", "Are you sure to delete this creation?", "Yes", "No"))
            {
                await _dialogService.ShowProgressDialogAsync(
                    false,
                    async (progressDialog, token) => await _creationManager.DeleteCreationAsync(Creation),
                    "Deleting...");

                await NavigationService.NavigateBackAsync();
            }
        }

        private async Task PlayAsync()
        {
            if (IsCreationPlayable())
            {
                await _dialogService.ShowMessageBoxAsync("Play!!!", null, "Ok");
            }
            else
            {
                await _dialogService.ShowMessageBoxAsync("Warning", "There are missing devices!", "Ok");
            }
        }

        private async Task AddControllerProfileAsync()
        {
            var result = await _dialogService.ShowInputDialogAsync("Controller profile", "Enter a profile name", null, "Profile name", "Create", "Cancel");
            if (result.IsOk)
            {
                if (string.IsNullOrWhiteSpace(result.Result))
                {
                    await DisplayAlertAsync("Warning", "Controller profile name can not be empty.", "Ok");
                    return;
                }

                ControllerProfile controllerProfile = null;
                await _dialogService.ShowProgressDialogAsync(
                    false,
                    async (progressDialog, token) => controllerProfile = await _creationManager.AddControllerProfileAsync(Creation, result.Result),
                    "Creating...");

                await NavigationService.NavigateToAsync<ControllerProfilePageViewModel>(new NavigationParameters(("controllerprofile", controllerProfile)));
            }
        }

        private bool IsCreationPlayable()
        {
            var deviceIds = Creation.GetDeviceIds();
            return deviceIds.All(di => _deviceManager.GetDeviceById(di) != null);
        }
    }
}
