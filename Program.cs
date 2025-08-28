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
            
            // Try the new location first (same directory as exe), then fall back to old location
            string resoniteDir = Path.GetDirectoryName(resoniteLocation);
            string LibraryFolder = resoniteDir;
            string oldLibraryFolder = Path.Combine(resoniteDir, "Resonite_Data", "Managed");
            
            Dictionary<string, Assembly> libraries = new Dictionary<string, Assembly>();
            try
            {
                // Try new location first (post-August update)
                if (File.Exists(Path.Combine(LibraryFolder, "Newtonsoft.Json.dll")) && 
                    File.Exists(Path.Combine(LibraryFolder, "Elements.Core.dll")))
                {
                    Console.WriteLine("Found DLLs in new location (same directory as exe)");
                    libraries.Add("Newtonsoft.Json", Assembly.LoadFrom(Path.Combine(LibraryFolder, "Newtonsoft.Json.dll")));
                    libraries.Add("Elements.Core", Assembly.LoadFrom(Path.Combine(LibraryFolder, "Elements.Core.dll")));
                }
                // Fall back to old location
                else if (File.Exists(Path.Combine(oldLibraryFolder, "Newtonsoft.Json.dll")) && 
                         File.Exists(Path.Combine(oldLibraryFolder, "Elements.Core.dll")))
                {
                    Console.WriteLine("Found DLLs in old location (Resonite_Data/Managed)");
                    libraries.Add("Newtonsoft.Json", Assembly.LoadFrom(Path.Combine(oldLibraryFolder, "Newtonsoft.Json.dll")));
                    libraries.Add("Elements.Core", Assembly.LoadFrom(Path.Combine(oldLibraryFolder, "Elements.Core.dll")));
                }
                else
                {
                    throw new FileNotFoundException("Required DLL files not found in either location");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load Resonite libraries: {ex.Message}");
                Console.WriteLine("Looking for:");
                Console.WriteLine($"  - {Path.Combine(LibraryFolder, "Newtonsoft.Json.dll")}");
                Console.WriteLine($"  - {Path.Combine(LibraryFolder, "Elements.Core.dll")}");
                Console.WriteLine("Or in old location:");
                Console.WriteLine($"  - {Path.Combine(oldLibraryFolder, "Newtonsoft.Json.dll")}");
                Console.WriteLine($"  - {Path.Combine(oldLibraryFolder, "Elements.Core.dll")}");
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
                Filter = "Resonite DataTree File (*.frdt;*.brson;*.lz4bson;*.7zbson)|*.frdt;*.brson;*.lz4bson;*.7zbson",
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
            if (ElementsCore == null)
            {
                Console.WriteLine("ERROR: Elements.Core assembly not loaded properly");
                return false;
            }
            
            // List all types in Elements.Core to help debug
            Console.WriteLine("Searching for DataTreeConverter in Elements.Core...");
            var dataTreeConverter = ElementsCore.GetType("Elements.Core.DataTreeConverter");
            
            if (dataTreeConverter == null)
            {
                Console.WriteLine("ERROR: DataTreeConverter not found in Elements.Core");
                Console.WriteLine("Available types containing 'DataTree' or 'Converter':");
                foreach (var type in ElementsCore.GetTypes())
                {
                    if (type.FullName.Contains("DataTree") || type.FullName.Contains("Converter"))
                    {
                        Console.WriteLine($"  - {type.FullName}");
                    }
                }
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return false;
            }
            
            var dataTreeLoad = dataTreeConverter.GetMethod("Load", new Type[] { typeof(string), typeof(string) });
            if (dataTreeLoad == null)
            {
                Console.WriteLine("ERROR: Load method not found in DataTreeConverter");
                Console.WriteLine("Available methods:");
                foreach (var method in dataTreeConverter.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    Console.WriteLine($"  - {method.Name}");
                }
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return false;
            }
            
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
            ConstructorInfo jsonTextWriterConstructor = NewtonsoftJson.GetType("Newtonsoft.Json.JsonTextWriter").GetConstructor(new Type[] {typeof(TextWriter)});
            object jsonTextWriter = jsonTextWriterConstructor.Invoke(new object[] { fileStream });
            var writemethod = dataTreeConverter.GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            writemethod.Invoke(null, new object[] { convert, jsonTextWriter });

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
