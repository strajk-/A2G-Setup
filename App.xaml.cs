using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;

namespace A2G_Setup
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            //Keeps Tooltips from being closed on a timer, instead stays shown until the user moves the cursor outside the boundaries of the control
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
            base.OnStartup(e);
        }
    }
}
