using BrickController2.PlatformServices.SharedFileStorage;
using System;
using System.IO;

namespace BrickController2.Windows.PlatformServices.SharedFileStorage;

public class SharedFileStorageService : ISharedFileStorageService
{
    private readonly string _sharedStorageDirectory = GetSharedStorageBaseDirectory();

    public bool IsSharedStorageAvailable => true;

    public bool IsPermissionGranted { get; set; }

    public string SharedStorageBaseDirectory => _sharedStorageDirectory;

    public string SharedStorageDirectory => SharedStorageBaseDirectory;

    private static string GetSharedStorageBaseDirectory()
    {
        // ApplicationData.Current.RoamingFolder.Path
        //
        // requires package identity (MSIX).
        // This app may run unpackaged, so ApplicationData.Current does not just throw a
        // catchable .NET exception here - it fails fast with a native
        // STATUS_STOWED_EXCEPTION (0xC000027B) inside the WinUI runtime, which crashes
        // the process before any try/catch around it can run. So we must not call it
        // at all in this (unpackaged) build - instead we compute an equivalent folder
        // ourselves.

        try
        {
            // Manual equivalent of ApplicationData.Current.RoamingFolder.Path for
            // unpackaged apps: packaged apps get an isolated folder under
            // %LOCALAPPDATA%\Packages\<PackageFamilyName>\RoamingState, which only
            // exists because of the package identity we don't have. We use our own
            // app-specific folder under %APPDATA% instead.
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrickController2");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            return null!;
        }
    }
}