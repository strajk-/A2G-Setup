using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace Localizer
{
    public class LocalizedText : MarkupExtension, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static HashSet<int> DisposedHashCodes = new HashSet<int>();
        private static readonly Dictionary<Language, Dictionary<string, string>> LocalizationCache = new Dictionary<Language, Dictionary<string, string>>();

        public FrameworkElement ParentElement { get; set; }

        private string _value;
        public string Value
        {
            get => _value;
            set {
                if (_value != value) {
                    _value = value;
                    NotifyPropertyChanged(nameof(Value));
                }
            }
        }

        private string _nr;
        public string Nr
        {
            get => _nr;
            set {
                if (_nr != value) {
                    _nr = value;
                    NotifyPropertyChanged(nameof(Nr));
                    UpdateLocalizedString();
                }
            }
        }

        private string _Format;
        public string Format
        {
            get => _Format;
            set {
                if (_Format != value) {
                    _Format = value;
                    NotifyPropertyChanged(nameof(Format));
                    UpdateLocalizedString();
                }
            }
        }

        public static event PropertyChangedEventHandler LanguageChanged;
        private static Language _language;
        public static Language Language
        {
            get => _language;
            set {
                if (_language != value) {
                    _language = value;
                    OnLanguageChanged();
                }
            }
        }

        public LocalizedText()
        {
            LanguageChanged += IndText_LanguageChanged;
        }

        private static void OnLanguageChanged()
        {
            LanguageChanged?.Invoke(null, new PropertyChangedEventArgs("Language"));
        }

        private void IndText_LanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ParentElement != null && ParentElement is IDisposable disposable) {
                if (LocalizedText.DisposedHashCodes.Contains(ParentElement.GetHashCode())) {
                    this.Dispose();
                }
            }

            UpdateLocalizedString();
        }

        ~LocalizedText()
        {
            Dispose(false);
        }

        bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed) {
                try {
                    ParentElement = null;
                    LanguageChanged -= IndText_LanguageChanged;
                }
                catch { }
                disposed = true;
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Retrieve the parent element of the IndText instance
            if (ParentElement == null) {
                DependencyObject currentElement = null;
                var service = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
                if (service != null) {
                    var targetObject = service.TargetObject as DependencyObject;
                    if (targetObject != null) {
                        currentElement = targetObject;
                        while (currentElement != null && !(currentElement is Window)) {
                            currentElement = LogicalTreeHelper.GetParent(currentElement);
                        }
                    }
                }
                if (currentElement != null) {
                    ParentElement = currentElement as FrameworkElement;
                }
            }

            // Check if localization for the selected language is already cached
            if (!LocalizationCache.ContainsKey(Language)) {
                LocalizationCache[Language] = LoadLocalizationData(Language);
            }

            // Retrieve localized text from memory cache based on the provided number
            if (!LocalizationCache[Language].TryGetValue(Nr, out string localizedValue)) {
                localizedValue = $"{Nr} not found";
            }

            // Apply custom formating if one was given
            if (string.IsNullOrEmpty(Format)) {
                Value = localizedValue;
            } else Value = string.Format(Format, localizedValue);

            // Create a binding with the localized string
            Binding binding = new Binding("Value") {
                Source = this // Bind to the current instance of IndText
            };

            // Return the binding expression
            return binding.ProvideValue(serviceProvider);
        }

        private void UpdateLocalizedString()
        {
            // Check if localization for the selected language is already cached
            if (!LocalizationCache.ContainsKey(Language)) {
                LocalizationCache[Language] = LoadLocalizationData(Language);
            }

            // Update localized string when Nr changes
            if (!LocalizationCache[Language].TryGetValue(Nr, out string localizedValue)) {
                localizedValue = $"{Nr} not found";
            }

            if (string.IsNullOrEmpty(Format)) {
                Value = localizedValue;
            } else Value = string.Format(Format, localizedValue);
        }

        private static void UpdateAllLocalizedStrings(LocalizedText instance)
        {
            // Update all localized strings when language changes
            foreach (var entry in LocalizationCache) {
                foreach (var localizedString in entry.Value) {
                    instance.NotifyPropertyChanged(nameof(instance.ProvideValue));
                }
            }
        }


        private static Dictionary<string, string> LoadLocalizationData(Language language)
        {
            // Load localization data for the selected language from the embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            string filename = GetFileNameForLanguage(language);
            string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(filename));

            var localizationData = new Dictionary<string, string>();
            if (resourceName != null) {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream)) {
                    while (!reader.EndOfStream) {
                        string line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line?.Trim()) || line.Trim().StartsWith("#")) continue;
                        string[] parts = line.Split('\t');
                        if (parts.Length == 2) {
                            string value = parts[1].Replace(@"\r\n", Environment.NewLine).Replace(@"\n", Environment.NewLine).Replace(@"\r", Environment.NewLine);
                            localizationData[parts[0].Trim()] = value.Trim();
                        }
                    }
                }
            } else {
                localizationData = LoadLocalizationData(Language.English);
            }

            return localizationData;
        }

        private static string GetFileNameForLanguage(Language language)
        {
            // Map the Language enum value to the corresponding filename
            // Adjust this logic according to your localization file naming convention
            switch (language) {
                case Language.German:
                    return "de.txt"; // German localization file
                case Language.English:
                    return "en.txt"; // English localization file
                default:
                    return "en.txt"; // Default to English if the language is not recognized
            }
        }

        static string lastNr = "0";
        public static string GetText () => GetText(lastNr);
        public static string GetText(int Nr) => GetText(Nr.ToString());
        public static string GetText(string Nr)
        {
            lastNr = Nr; // For Language change of static elements, such as text on a ProgressBar, running GetText() will refetch this value
            if (!LocalizationCache.ContainsKey(Language)) {
                LocalizationCache[Language] = LoadLocalizationData(Language);
                return GetText(Nr); // try again
            } else {
                if (!LocalizationCache[Language].TryGetValue(Nr, out string localizedValue)) {
                    localizedValue = $"{Nr} not found";
                }
                return localizedValue;
            }
        }

        public static void InitLanguage()
        {
            if (!LocalizationCache.ContainsKey(Language)) {
                LocalizationCache[Language] = LoadLocalizationData(Language);
            }
        }
    }

    public enum Language
    {
        [Description("Deutsch")]
        German,
        [Description("English")]
        English
    }
}
