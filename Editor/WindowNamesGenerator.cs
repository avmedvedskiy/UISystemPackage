using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UISystem.Editor
{
    [InitializeOnLoad]
    public static class WindowNamesGenerator
    {
        private const string PATH_TO_GENERATED_FILE = "Assets/Scripts/Generated";
        private const string GENERATED_FILE_NAME = PATH_TO_GENERATED_FILE + "/Windows.cs";


        static WindowNamesGenerator()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        private static void OnCompilationFinished(string s, CompilerMessage[] compilerMessages)
        {
            var allWindows = FindAllWindows();

            string constantsFile =
                $@"//This code is autogenerated 
                    using Cysharp.Threading.Tasks;
                    namespace UISystem{{
                    public static class Windows {{
                    {
                        $"{string.Join("\r\n", CreateWindowConstants(allWindows))}" +
                        "\r\n" +
                        $"{string.Join("\r\n", CreateWindowExtensionsMethods(allWindows))}"
                    }
                }}
            }}";


            Directory.CreateDirectory(PATH_TO_GENERATED_FILE);
            File.WriteAllText(GENERATED_FILE_NAME, constantsFile);
        }

        private static IEnumerable<string> CreateWindowExtensionsMethods(List<IClosedWindow> allWindows) =>
            allWindows.Select(ConvertToExtensionMethod);

        private static string ConvertToExtensionMethod(IClosedWindow window)
        {
            return ImplementsGenericInterface(window.GetType(), typeof(IResultWindow<>))
                ? ConvertResultWindow(window)
                : ConvertDefaultWindow(window);
        }

        private static string ConvertResultWindow(IClosedWindow window)
        {
            var type = window.GetType();
            var name = window.gameObject.name;
            var resultType = GetInterface(type, typeof(IResultWindow<>));
            var payloadDataType = GenericTypeArgumentDeep(type);
            var resultDataType = GenericTypeArgumentDeep(resultType);
            var constName = ConvertToConstantName(name);
            return
                $"public static UniTask<{resultDataType.FullName}> Open{name}Async(this IWindowService service, {payloadDataType.FullName} payload = default)" +
                $"=> service.OpenPopup<{type.FullName}, {payloadDataType.FullName},{resultDataType.FullName}>({constName},payload);";
        }

        private static string ConvertDefaultWindow(IClosedWindow window)
        {
            var type = window.GetType();
            var name = window.gameObject.name;
            var payloadDataType = GenericTypeArgumentDeep(type);
            var constName = ConvertToConstantName(name);
            return
                $"public static UniTask<{type.FullName}> Open{name}Async(this IWindowService service, {payloadDataType.FullName} payload = default, bool inQueue = false)" +
                $"=> service.OpenAsync<{type.FullName}, {payloadDataType.FullName}>({constName},payload,inQueue); \r\n" +
                
                $"public static UniTask Close{name}Async(this IWindowService service) => service.CloseAsync({constName});";
        }
        

        private static IEnumerable<string> CreateWindowConstants(List<IClosedWindow> allWindows)
        {
            var windowConstants = allWindows
                .Select(value =>
                    $"public const string {ConvertToConstantName(value.gameObject.name)} = \"{value.gameObject.name}\"; ");
            return windowConstants;
        }


        private static string ConvertToConstantName(string input) =>
            string
                .Join('_', Regex.Split(input, @"(?<!^)(?=[A-Z])"))
                .ToUpper();

        private static List<IClosedWindow> FindAllWindows()
        {
            List<IClosedWindow> windows = new();
            var paths = AssetDatabase.FindAssets($"t:GameObject");

            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(path));
                if (asset.TryGetComponent<IClosedWindow>(out var window))
                {
                    windows.Add(window);
                }
            }

            windows.Sort((x, y) => string.CompareOrdinal(x.gameObject.name, y.gameObject.name));
            return windows;
        }

        private static Type GenericTypeArgumentDeep(Type type)
        {
            if (type == null)
                return null;

            return type.GenericTypeArguments.Length > 0
                ? type.GenericTypeArguments[0]
                : GenericTypeArgumentDeep(type.BaseType);
        }

        private static Type GetInterface(Type classType, Type genericInterfaceType)
        {
            return classType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType);
            ;
        }

        private static bool ImplementsGenericInterface(Type classType, Type genericInterfaceType)
        {
            return classType.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType);
        }
    }
}