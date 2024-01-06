using System;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;


namespace Resonite_DataTree_Converter
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
        start_find:
            string settingsLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lexevolution", "Resonite DataTree Converter", "app.config");
            if (!File.Exists(settingsLocation))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsLocation));
                Console.WriteLine("Please find Resonite.exe");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                OpenFileDialog ofd = new OpenFileDialog()
                {
                    Filter = "Executable Files (*.exe)|*.exe",
                    Title = "Please select Resonite.exe",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false
                };
                DialogResult result = ofd.ShowDialog();
                Console.WriteLine("");
                if (result == DialogResult.Cancel)
                {
                    Console.WriteLine("Cancelled, restarting...");
                    Console.WriteLine("");
                    goto start_find;
                }
                File.WriteAllText(settingsLocation, ofd.FileName);
            }
            
            StreamReader sr = new StreamReader(settingsLocation);
            string resoniteLocation = sr.ReadToEnd();
            sr.Close();
            Console.WriteLine(string.Format("DIRECTORY: {0}", Path.GetDirectoryName(resoniteLocation)));
            string LibraryFolder = Path.Combine(Path.GetDirectoryName(resoniteLocation), "Resonite_Data", "Managed");
            Dictionary<string, Assembly> libraries = new Dictionary<string, Assembly>();
            try
            {
                libraries.Add("Newtonsoft.Json", Assembly.LoadFrom(Path.Combine(LibraryFolder, "Newtonsoft.Json.dll")));
                libraries.Add("Elements.Core", Assembly.LoadFrom(Path.Combine(LibraryFolder, "Elements.Core.dll")));
            }
            catch
            {
                Console.WriteLine("Resonite.exe not detected. Restarting...");
                Console.WriteLine("");
                File.Delete(settingsLocation);
                goto start_find;
            }

        start_select:
            Console.WriteLine("Which way do you want to convert the file? (press 1 or 2)");
            Console.WriteLine("1. DataTree to JSON");
            Console.WriteLine("2. JSON to DataTree");
            Console.WriteLine("q to quit");
            ConsoleKeyInfo test = Console.ReadKey();
            bool success;
            switch (test.Key)
            {
                case ConsoleKey.D1:
                    success = ToJSON(libraries);
                    if (!success) { goto start_select; }
                    break;
                case ConsoleKey.D2:
                    success = ToDataTree(libraries);
                    if (!success) { goto start_select; }
                    break;
                case ConsoleKey.Q:
                    break;
                default:
                    Console.WriteLine("Unsupported choice. Please press 1 or 2.");
                    Console.WriteLine("");
                    goto start_select;
            }
        }

        static bool ToJSON(Dictionary<string, Assembly> libraries)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "Resonite DataTree File (*.brson;*.lz4bson;*.7zbson)|*.brson;*.lz4bson;*.7zbson",
                Title = "Choose a DataTree file for conversion",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            DialogResult result = ofd.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                Console.WriteLine("Cancelled opening file. Restarting.");
                Console.WriteLine("");
                return false;
            }
            libraries.TryGetValue("Elements.Core", out Assembly ElementsCore);
            var dataTreeConverter = ElementsCore.GetType("Elements.Core.DataTreeConverter");
            var dataTreeLoad = dataTreeConverter.GetMethod("Load", new Type[] { typeof(string), typeof(string) });
            object convert = dataTreeLoad.Invoke(null, new object[] { ofd.FileName, null });
            //DataTreeDictionary convert = DataTreeConverter.Load(ofd.FileName);

            SaveFileDialog save = new SaveFileDialog()
            {
                Filter = "JSON File|*.json",
                Title = "Save Output JSON to...",
                OverwritePrompt = true
            };
            result = save.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                Console.WriteLine("Cancelled saving file. Restarting.");
                Console.WriteLine("");
                return false;
            }

            StreamWriter fileStream = File.CreateText(save.FileName);

            libraries.TryGetValue("Newtonsoft.Json", out Assembly NewtonsoftJson);
            Console.WriteLine(NewtonsoftJson.GetTypes());
            ConstructorInfo aaaaaa = NewtonsoftJson.GetType("Newtonsoft.Json.JsonTextWriter").GetConstructor(new Type[] {typeof(TextWriter)});
            object testy = aaaaaa.Invoke(new object[] { fileStream });
            //object testy = NewtonsoftJson.GetType("JsonTextWriter").GetConstructor(new Type[] { NewtonsoftJson.GetType("Newtonsoft.Json.JsonTextWriter") }).Invoke(null, new object[] { fileStream });
            //JsonTextWriter test = new JsonTextWriter(fileStream);
            var writemethod = dataTreeConverter.GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            writemethod.Invoke(null, new object[] { convert, testy });

            fileStream.Dispose();
            fileStream.Close();

            return true;
        }

        static bool ToDataTree(Dictionary<string, Assembly> libraries)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "JSON File (*.json)|*.json",
                Title = "Choose a JSON file for conversion",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            DialogResult result = ofd.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                Console.WriteLine("Cancelled opening file. Restarting.");
                Console.WriteLine("");
                return false;
            }

            libraries.TryGetValue("Elements.Core", out Assembly ElementsCore);

            Type dataTreeConverter = ElementsCore.GetType("Elements.Core.DataTreeConverter");

            TextReader testReader = new StreamReader(ofd.FileName);

            libraries.TryGetValue("Newtonsoft.Json", out Assembly Newtonsoft);
            ConstructorInfo JsonTextReaderConstructor = Newtonsoft.GetType("Newtonsoft.Json.JsonTextReader").GetConstructor(new Type[] {typeof(TextReader)});
            object JsonTextReader = JsonTextReaderConstructor.Invoke(new object[] { testReader });

            MethodInfo JsonConvertMethod = dataTreeConverter.GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);

            object DataTree = JsonConvertMethod.Invoke(null, new object[] { JsonTextReader });

            SaveFileDialog save = new SaveFileDialog()
            {
                Filter = "Brotli Compressed BSON (*.brson)|*.brson|LZ4 Compressed BSON (*.lz4bson)|*.lz4bson|7-Zip compressed BSON (*.7zbson)|*.7zbson",
                Title = "Save DataTree file to...",
                OverwritePrompt = true,
                AddExtension = true
            };
            result = save.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                Console.WriteLine("Cancelled saving file. Restarting.");
                Console.WriteLine("");
                return false;
            }

            MethodInfo DataTreeSaveMethod = dataTreeConverter.GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);

            Type compressionType = ElementsCore.GetType("Elements.Core.DataTreeConverter+Compression");
            Array compressionTypeValues = compressionType.GetEnumValues();
            object selectedCompressionType;
            switch (Path.GetExtension(save.FileName))
            {
                case ".brson":
                    selectedCompressionType = compressionTypeValues.GetValue(3);
                    break;
                case ".7zbson":
                    selectedCompressionType = compressionTypeValues.GetValue(2);
                    break;
                case ".lz4bson":
                    selectedCompressionType = compressionTypeValues.GetValue(1);
                    break;
                default:
                    selectedCompressionType = compressionTypeValues.GetValue(0);
                    break;
            }

            DataTreeSaveMethod.Invoke(null, new object[] {DataTree, save.FileName, selectedCompressionType});
            return true;
        }
    }
}
