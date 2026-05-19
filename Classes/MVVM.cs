using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerMemberNameAttribute : Attribute
    {
    }
}

namespace A2G_Setup
{
    public class NotifyingWindow : Window, INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        protected T Access<T> (Func<T> getter) => UIHelper.Access(getter);

        protected void Access (Action action) => UIHelper.Access(action);

        protected void NotifyPropertyChanged ([CallerMemberName] string propertyName = null)
            => UIHelper.Raise(PropertyChanged, this, propertyName);

        protected void NotifyPropertyChanged<T> (ref T field, T value, [CallerMemberName] string propertyName = null)
            => UIHelper.UpdateAndRaise(PropertyChanged, this, ref field, value, propertyName);

        protected void SetExternal<T> (Func<T> getter, Action<T> setter, T value, [CallerMemberName] string propertyName = null)
            => UIHelper.SetExternal(PropertyChanged, this, getter, setter, value, propertyName);
    }

    public static class UIHelper
    {
        public static T Access<T> (Func<T> getter)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess())
                return getter();

            return (T)Application.Current.Dispatcher.Invoke(getter);
        }

        public static void Access (Action action)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess())
                action();
            else
                Application.Current.Dispatcher.Invoke(action);
        }

        public static void Raise (PropertyChangedEventHandler handler, object sender, string propertyName)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess()) {
                handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            } else {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName))));
            }
        }

        public static void UpdateAndRaise<T> (PropertyChangedEventHandler handler, object sender, ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;

            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess()) {
                handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            } else {
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
                }));
            }
        }

        public static void SetExternal<T> (PropertyChangedEventHandler handler, object sender, Func<T> getter, Action<T> setter, T value, string propertyName)
        {
            Access(() => {
                if (!EqualityComparer<T>.Default.Equals(getter(), value)) {
                    setter(value);
                    Raise(handler, sender, propertyName);
                }
            });
        }
    }
}
