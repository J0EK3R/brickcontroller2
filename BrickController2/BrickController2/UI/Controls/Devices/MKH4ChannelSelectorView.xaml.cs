using BrickController2.DeviceManagement;
using Microsoft.Maui.Controls.Xaml;

namespace BrickController2.UI.Controls.Devices;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MKH4ChannelSelectorView : DeviceChannelSelectorViewBase, IDeviceChannelSelectorView
{
    public static DeviceType DeviceType => DeviceType.MKH4;
    // Buttons intentionally show MK4 icons — preserved from original XAML
    protected override DeviceType SelectorDeviceType => DeviceType.MKH4;

    public MKH4ChannelSelectorView()
    {
        InitializeComponent();
        RegisterChannelButtons(MKH4Channel0, MKH4Channel1, MKH4Channel2, MKH4Channel3);
    }
}
