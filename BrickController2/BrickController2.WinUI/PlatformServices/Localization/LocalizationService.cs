﻿using BrickController2.PlatformServices.Localization;
using Microsoft.Maui.Controls;
using System.Globalization;
using System.Threading;

[assembly: Dependency(typeof(BrickController2.Windows.PlatformServices.Localization.LocalizationService))]
namespace BrickController2.Windows.PlatformServices.Localization;

public class LocalizationService : ILocalizationService
{
    public CultureInfo CurrentCultureInfo
    {
        get
        {
            return CultureInfo.CurrentUICulture;
        }

        set
        {
            CultureInfo.CurrentUICulture = value;
            Thread.CurrentThread.CurrentCulture = value;
            Thread.CurrentThread.CurrentUICulture = value;
        }
    }
}