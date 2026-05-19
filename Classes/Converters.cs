using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace A2G_Setup
{
    public class VersionToBoolConverter : IValueConverter
    {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Version selectedVersion && parameter is string param) {
                switch (param.ToLower()) {
                    case "otvdm":
                        return selectedVersion == Version.A2G;

                    case "klite":
                        return selectedVersion == Version.A2G || selectedVersion == Version.A2007;

                    case "wined3d":
                        return selectedVersion == Version.A2G || selectedVersion == Version.A2007;

                    case "16bitcol":
                        return selectedVersion == Version.A2G || selectedVersion == Version.A2007;

                    default:
                        return true;
                }
            }

            return false;
        }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val) {
                return val ? Visibility.Visible : Visibility.Hidden;
            }

            return Visibility.Hidden;
        }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolInverterConverter : IValueConverter
    {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val) {
                return !val;
            }

            return false;
        }

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
