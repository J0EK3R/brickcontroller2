﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BrickController2.BusinessLogic;
using BrickController2.CreationManagement;
using BrickController2.CreationManagement.Sharing;
using BrickController2.Helpers;
using BrickController2.PlatformServices.SharedFileStorage;
using BrickController2.UI.Commands;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Navigation;
using BrickController2.UI.Services.Translation;

namespace BrickController2.UI.ViewModels
{
    public class CreationPageViewModel : PageViewModelBase
    {
        private readonly ICreationManager _creationManager;
        private readonly IDialogService _dialogService;
        private readonly IPlayLogic _playLogic;
        private readonly ISharingManager<Creation> _sharingManager;
        private readonly ISharingManager<ControllerProfile> _sharingManagerProfile;
        private CancellationTokenSource? _disappearingTokenSource;

        public CreationPageViewModel(
            INavigationService navigationService,
            ITranslationService translationService,
            ICreationManager creationManager,
            IDialogService dialogService,
            ISharedFileStorageService sharedFileStorageService,
            IPlayLogic playLogic,
            ISharingManager<Creation> sharingManager,
            ISharingManager<ControllerProfile> sharingManagerProfile,
            NavigationParameters parameters)
            : base(navigationService, translationService)
        {
            _creationManager = creationManager;
            _dialogService = dialogService;
            SharedFileStorageService = sharedFileStorageService;
            _playLogic = playLogic;
            _sharingManager = sharingManager;
            _sharingManagerProfile = sharingManagerProfile;
            Creation = parameters.Get<Creation>("creation");

            ImportControllerProfileCommand = new SafeCommand(async () => await ImportControllerProfileAsync(), () => SharedFileStorageService.IsSharedStorageAvailable);
            PasteControllerProfileCommand = new SafeCommand(PasteControllerProfileAsync);
            ExportCreationCommand = new SafeCommand(async () => await ExportCreationAsync(), () => SharedFileStorageService.IsSharedStorageAvailable);
            CopyCreationCommand = new SafeCommand(CopyCreationAsync);
            RenameCreationCommand = new SafeCommand(async () => await RenameCreationAsync());
            PlayCommand = new SafeCommand(async () => await PlayAsync());
            AddControllerProfileCommand = new SafeCommand(async () => await AddControllerProfileAsync());
            ControllerProfileTappedCommand = new SafeCommand<ControllerProfile>(async controllerProfile => await NavigationService.NavigateToAsync<ControllerProfilePageViewModel>(new NavigationParameters(("controllerprofile", controllerProfile))));
            DeleteControllerProfileCommand = new SafeCommand<ControllerProfile>(async controllerProfile => await DeleteControllerProfileAsync(controllerProfile));
        }

        public Creation Creation { get; }

        public ISharedFileStorageService SharedFileStorageService { get; }
        public ICommand ImportControllerProfileCommand { get; }
        public ICommand PasteControllerProfileCommand { get; }
        public ICommand ExportCreationCommand { get; }
        public ICommand CopyCreationCommand { get; }
        public ICommand RenameCreationCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand AddControllerProfileCommand { get; }
        public ICommand ControllerProfileTappedCommand { get; }
        public ICommand DeleteControllerProfileCommand { get; }

        public override void OnAppearing()
        {
            _disappearingTokenSource?.Cancel();
            _disappearingTokenSource = new CancellationTokenSource();
        }

        public override void OnDisappearing()
        {
            _disappearingTokenSource?.Cancel();
        }

        private async Task RenameCreationAsync()
        {
            try
            {
                var result = await _dialogService.ShowInputDialogAsync(
                    Creation.Name,
                    Translate("CreationName"),
                    Translate("Rename"),
                    Translate("Cancel"),
                    KeyboardType.Text,
                    (creationName) => !string.IsNullOrEmpty(creationName),
                    _disappearingTokenSource?.Token ?? default);
                if (result.IsOk)
                {
                    if (string.IsNullOrWhiteSpace(result.Result))
                    {
                        await _dialogService.ShowMessageBoxAsync(
                            Translate("Warning"),
                            Translate("CreationNameCanNotBeEmpty"),
                            Translate("Ok"),
                            _disappearingTokenSource?.Token ?? default);
                        return;
                    }

                    await _dialogService.ShowProgressDialogAsync(
                        false,
                        async (progressDialog, token) => await _creationManager.RenameCreationAsync(Creation, result.Result),
                        Translate("Renaming"),
                        token: _disappearingTokenSource?.Token ?? default);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task PlayAsync()
        {
            try
            {
                var validationResult = _playLogic.ValidateCreation(Creation);

                string warning = string.Empty;
                switch (validationResult)
                {
                    case CreationValidationResult.MissingControllerAction:
                        warning = Translate("NoControllerActions");
                        break;

                    case CreationValidationResult.MissingDevice:
                        warning = Translate("MissingDevices");
                        break;

                    case CreationValidationResult.MissingSequence:
                        warning = Translate("MissingSequence");
                        break;
                }

                if (validationResult == CreationValidationResult.Ok)
                {
                    await NavigationService.NavigateToAsync<PlayerPageViewModel>(new NavigationParameters(("creation", Creation)));
                }
                else
                {
                    await _dialogService.ShowMessageBoxAsync(
                        Translate("Warning"),
                        warning,
                        Translate("Ok"),
                        _disappearingTokenSource?.Token ?? default);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task AddControllerProfileAsync()
        {
            try
            {
                var result = await _dialogService.ShowInputDialogAsync(
                    string.Empty,
                    Translate("ProfileName"),
                    Translate("Create"),
                    Translate("Cancel"),
                    KeyboardType.Text,
                    (profileName) => !string.IsNullOrEmpty(profileName),
                    _disappearingTokenSource?.Token ?? default);

                if (result.IsOk)
                {
                    if (string.IsNullOrWhiteSpace(result.Result))
                    {
                        await _dialogService.ShowMessageBoxAsync(
                            Translate("Warning"),
                            Translate("ProfileNameCanNotBeEmpty"),
                            Translate("Ok"),
                            _disappearingTokenSource?.Token ?? default);

                        return;
                    }

                    ControllerProfile? controllerProfile = null;
                    await _dialogService.ShowProgressDialogAsync(
                        false,
                        async (progressDialog, token) => controllerProfile = await _creationManager.AddControllerProfileAsync(Creation, result.Result),
                        Translate("Creating"),
                        token: _disappearingTokenSource?.Token ?? default);

                    await NavigationService.NavigateToAsync<ControllerProfilePageViewModel>(new NavigationParameters(("controllerprofile", controllerProfile!)));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task DeleteControllerProfileAsync(ControllerProfile controllerProfile)
        {
            try
            {
                if (await _dialogService.ShowQuestionDialogAsync(
                    Translate("Confirm"),
                    $"{Translate("AreYouSureToDeleteProfile")} '{controllerProfile.Name}'?",
                    Translate("Yes"),
                    Translate("No"),
                    _disappearingTokenSource?.Token ?? default))
                {
                    await _dialogService.ShowProgressDialogAsync(
                        false,
                        async (progressDialog, token) => await _creationManager.DeleteControllerProfileAsync(controllerProfile),
                        Translate("Deleting"),
                        token: _disappearingTokenSource?.Token ?? default);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ImportControllerProfileAsync()
        {
            try
            {
                var controllerProfileFilesMap = FileHelper.EnumerateDirectoryFilesToFilenameMap(SharedFileStorageService.SharedStorageDirectory!, $"*.{FileHelper.ControllerProfileFileExtension}");
                if (controllerProfileFilesMap?.Any() ?? false)
                {
                    var result = await _dialogService.ShowSelectionDialogAsync(
                        controllerProfileFilesMap.Keys,
                        Translate("ControllerProfile"),
                        Translate("Cancel"),
                        _disappearingTokenSource?.Token ?? default);

                    if (result.IsOk)
                    {
                        try
                        {
                            await _creationManager.ImportControllerProfileAsync(Creation, controllerProfileFilesMap[result.SelectedItem]);
                        }
                        catch (Exception)
                        {
                            await _dialogService.ShowMessageBoxAsync(
                                Translate("Error"),
                                Translate("FailedToImportControllerProfile"),
                                Translate("Ok"),
                                _disappearingTokenSource?.Token ?? default);
                        }
                    }
                }
                else
                {
                    await _dialogService.ShowMessageBoxAsync(
                        Translate("Information"),
                        Translate("NoProfilesToImport"),
                        Translate("Ok"),
                        _disappearingTokenSource?.Token ?? default);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        private async Task PasteControllerProfileAsync()
        {
            try
            {
                var profile = await _sharingManagerProfile.ImportFromClipboardAsync();
                await _creationManager.ImportControllerProfileAsync(Creation, profile);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageBoxAsync(
                    Translate("Error"),
                    Translate("FailedToImportControllerProfile", ex),
                    Translate("Ok"),
                    _disappearingTokenSource?.Token ?? default);
            }
        }

        private async Task ExportCreationAsync()
        {
            try
            {
                var filename = Creation.Name;
                var done = false;

                do
                {
                    var result = await _dialogService.ShowInputDialogAsync(
                        filename,
                        Translate("CreationName"),
                        Translate("Ok"),
                        Translate("Cancel"),
                        KeyboardType.Text,
                        fn => FileHelper.FilenameValidator(fn),
                        _disappearingTokenSource?.Token ?? default);

                    if (!result.IsOk)
                    {
                        return;
                    }

                    filename = result.Result;
                    var filePath = Path.Combine(SharedFileStorageService.SharedStorageDirectory!, $"{filename}.{FileHelper.CreationFileExtension}");

                    if (!File.Exists(filePath) || 
                        await _dialogService.ShowQuestionDialogAsync(
                            Translate("FileAlreadyExists"),
                            Translate("DoYouWantToOverWrite"),
                            Translate("Yes"),
                            Translate("No"),
                            _disappearingTokenSource?.Token ?? default))
                    {
                        try
                        {
                            await _creationManager.ExportCreationAsync(Creation, filePath);
                            done = true;

                            await _dialogService.ShowMessageBoxAsync(
                                Translate("ExportSuccessful"),
                                filePath,
                                Translate("Ok"),
                                _disappearingTokenSource?.Token ?? default);
                        }
                        catch (Exception)
                        {
                            await _dialogService.ShowMessageBoxAsync(
                                Translate("Error"),
                                Translate("FailedToExportCreation"),
                                Translate("Ok"),
                                _disappearingTokenSource?.Token ?? default);
                            
                            return;
                        }
                    }
                }
                while (!done);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task CopyCreationAsync()
            => _sharingManager.ShareToClipboardAsync(Creation);
    }
}
