using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace LineCount
{
    class Program
    {
        enum CountLineStat
        {
            CLS_NONE,
            CLS_COMMENT,
            CLS_MACRO,
            CLS_NEST_MACRO,
        };

        public class Options
        {
            [Value(0, HelpText = "answer if module A is referenced by module B(refModules), and where are the reference points")]
            public string filePath { get; set; } = ".";

            [Option('s', "skipMacro", Required = false, HelpText ="skip code surrounded by macros like #if XXXX ... #endif")]
            public string skipMacroString { get; set; } = "";

            [Option('e', "exclusive", Required = false, HelpText ="exclusive files or dirs")]
            public string exclusiveDirString { get; set; } = "";

            [Option('m', "modules", Required = true, HelpText ="modules A dirs")]
            public string moduleDirString { get; set; } = "";            

            [Option('r', "ref-modules", Required = true, HelpText ="ref modules B dirs")]
            public string refModuleDirString { get; set; } = "";

            [Option('i', "showInternalClass", Required = false, HelpText = "show internal class")]
            public bool bShowInternalClass { get; set; } = false;

            //[Option('r', "recursive", Required = false, HelpText = "recursive counting in dir")]
            //public bool bRecursive { get; set; } = true;

            [Option('v', "verbos", Required = false, HelpText = "showing detail result")]
            public bool bVerbos { get; set; } = false;

            [Option('d', "debug", Required = false, HelpText = "debug tool")]
            public bool bDebug { get; set; } = false;

        }
        static void DBG(string format, int lineNum, string line)
        { 
            if(bVerbos && bDebug)
                Console.WriteLine(format, lineNum, line);
        }

        static bool bVerbos = false;
        static bool bByFile = false;
        static bool bDebug = false;
        static bool bShowInternalClass = false;

        static string[] fileExts = { ".cpp", ".h", ".c", ".hpp", ".inl"};
        static string[] exclusiveDirs = { };
        static string[] moduleDirs = { };
        static string[] refModuleDirs = { };

        static HashSet<string> skipMacros = new HashSet<string>();

        static Dictionary<string, string> RefHeaderDict = new Dictionary<string, string>(); //文件名和文件路径的映射
        static Dictionary<string, string> ClassDict = new Dictionary<string, string>(); //类名和类定义文件路径的映射， <class, defFilePath@lineNum>
        
        //统计一个类都被哪些源文件引用了。 
        static Dictionary<string, List<string>> ClassRefDict = new Dictionary<string, List<string>>(); //引用关系, key被value引用，<class, [refFilePath@lineNum]>, 

        static void FindRefInFile(string filePath)
        {
            int lineNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    Stack<string> macroNameStack = new Stack<string>();
                    macroNameStack.Push("");
                    string curMacroName = macroNameStack.Peek();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();

                    List<int> macroCountStack = new List<int>();
                    macroCountStack.Add(0);

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;

                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            DBG("{0:0000} BL: {1}", lineNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            DBG("{0:0000} CM: {1}", lineNum, line);

                            continue;
                        }

                        if (curStat == CountLineStat.CLS_COMMENT)
                        {
                            if (line.Contains("*/"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} CM: {1}", lineNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            bool bStepIn = line.StartsWith("#if");

                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();

                                int innerMC = macroCountStack[0];
                                macroCountStack.RemoveAt(0);
                            }

                            if (!bStepIn)
                            {
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                DBG("{0:0000} CM: {1}", lineNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                DBG("{0:0000} C-: {1}", lineNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} C-: {1}", lineNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            macroCountStack.Insert(0, 1);
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach (string macro in skipMacros)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        DBG("{0:0000} MC: {1}", lineNum, line);
                                        isskipMacro = true;
                                        break;
                                    }
                                }
                                if (isskipMacro)
                                    continue;
                            }
                        }
                        if (line.StartsWith("#elif") || line.StartsWith("#else"))
                        {
                            //do not handle it yet.
                        }
                        if (line.StartsWith("#include") || //include行，忽略
                           (line.StartsWith("class") && line.Contains(";"))) //前置声明行，忽略
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            continue;
                        }

                        {
                            string[] actualLine = line.Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries); //移除行尾注释
                            if (actualLine.Length > 0)
                            {
                                string[] arrstr = actualLine[0].Split(new char[] { ' ', '/', '<', '>', '\"', '*', '(', ')', ';', ',', '=', '{', '}', '[', ']', '&', '|', ':', '.', '-', '+', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string word in arrstr)
                                {
                                    if (ClassDict.ContainsKey(word))
                                    {
                                        if (!ClassRefDict.ContainsKey(word))
                                        {
                                            ClassRefDict.Add(word, new List<string>());
                                        }
                                        ClassRefDict[word].Add(Path.GetFileName(filePath) + ":" + lineNum.ToString() + ":" + line);
                                        DBG("{0:0000} RF: {1}", lineNum, line);
                                        continue;
                                    }
                                }
                            }
                        }

                        DBG("{0:0000} CO: {1}", lineNum, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error：{ex.Message}");
            }
        }

        static bool ShouldExclude(string fileDir)
        {
            foreach (string exclusiveDir in exclusiveDirs)
            {
                if (exclusiveDir.Length == 0)
                    continue;

                string lowerDir = fileDir.ToLower();
                if (lowerDir.EndsWith(exclusiveDir))
                {
                    return true;
                }
            }
            return false;
        }
        //1. 递归查找fileDir下所有.h结尾的文件，保存到RefHeaderDict中。
        static void FindAllHeaderInDirectory(string fileDir, bool bRecursive)
        {
            //foreach (string exclusiveDir in exclusiveDirs)
            //{
            //    if (exclusiveDir.Length == 0)
            //        continue;

            //    string lowerDir = fileDir.ToLower();
            //    if (lowerDir.EndsWith(exclusiveDir))
            //    {
            //        return;
            //    }
            //}
            if (ShouldExclude(fileDir))
                return;

            if (Directory.Exists(fileDir))
            {
                string[] filePaths = Directory.GetFiles(fileDir);
                foreach (string filePath in filePaths)
                {
                    string ext = Path.GetExtension(filePath);
                    if (ext is null)
                        continue;
                    ext = ext.ToLower();

                    if (ShouldExclude(filePath))
                        continue;

                    string fileName = Path.GetFileName(filePath);

                    if (ext == ".h")
                    {
                        if (!RefHeaderDict.ContainsKey(fileName))
                        {
                            RefHeaderDict.Add(fileName, filePath);
                        }
                        else
                        {
                            Console.WriteLine("Error --- file exist in RefHeaderDict:", RefHeaderDict[fileName]);
                        }
                    }
                }

                if (bRecursive)
                {
                    // Recurse sub directories
                    string[] folders = Directory.GetDirectories(fileDir);
                    foreach (string folder in folders)
                    {
                        FindAllHeaderInDirectory(folder, bRecursive);
                    }
                }
            }
        }
        //2. 从头文件中找到所有类定义，忽略前置声明。
        static void FindClassesInHeaderFile(string filePath)
        {
            int lineNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    Stack<string> macroNameStack = new Stack<string>();
                    macroNameStack.Push("");
                    string curMacroName = macroNameStack.Peek();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();

                    List<int> macroCountStack = new List<int>();
                    macroCountStack.Add(0);

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;

                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            DBG("{0:0000} BL: {1}", lineNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            DBG("{0:0000} CM: {1}", lineNum, line);

                            continue;
                        }

                        if (curStat == CountLineStat.CLS_COMMENT)
                        {
                            if (line.Contains("*/"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} CM: {1}", lineNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            bool bStepIn = line.StartsWith("#if");

                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();

                                int innerMC = macroCountStack[0];
                                macroCountStack.RemoveAt(0);
                            }

                            if (!bStepIn)
                            {
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                DBG("{0:0000} CM: {1}", lineNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                DBG("{0:0000} C-: {1}", lineNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} C-: {1}", lineNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            macroCountStack.Insert(0, 1);
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach (string macro in skipMacros)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        DBG("{0:0000} MC: {1}", lineNum, line);
                                        isskipMacro = true;
                                        break;
                                    }
                                }
                                if (isskipMacro)
                                    continue;
                            }
                        }
                        if (line.StartsWith("#elif") || line.StartsWith("#else"))
                        {
                            //do not handle it yet.
                        }

                        if (line.StartsWith("struct"))
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            if (!line.Contains(";") && !line.Contains(")"))//前置声明 和 函数中的前置声明
                            {
                                string[] arrstr = line.Split(new char[] { ' ','\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string className = "";
                                if (arrstr.Length > 1)
                                {
                                    if (arrstr[1].Contains("_API"))
                                    {
                                        if (arrstr.Length > 2)
                                        {
                                            className = arrstr[2];
                                        }
                                    }
                                    else
                                    {
                                        className = arrstr[1];
                                    }
                                    DBG("{0:0000} ST: {1}", lineNum, className);
                                    if (!ClassDict.ContainsKey(className))
                                    {
                                        ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                    }
                                    continue;
                                }
                            }
                        }

                        if (line.StartsWith("enum"))
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            if (!line.Contains(";") && !line.Contains("\\")) //(;)内部用, (\)在define中
                            {
                                string[] arrstr = line.Split(new char[] { ' ', '{', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string className = "";
                                if (arrstr.Length > 1)
                                {
                                    if (arrstr[1].Contains("class"))
                                    {
                                        if (arrstr.Length > 2)
                                        {
                                            className = arrstr[2];
                                        }
                                    }
                                    else
                                    {
                                        className = arrstr[1];
                                    }
                                    DBG("{0:0000} EN: {1}", lineNum, className);
                                    if (!ClassDict.ContainsKey(className))
                                    {
                                        ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                    }
                                    continue;
                                }
                            }
                        }
                        if (line.StartsWith("namespace")) //type defined in namespace
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            if (!line.Contains(";") && !line.Contains("\\")) //(;)内部用, (\)在define中
                            {
                                string[] arrstr = line.Split(new char[] { ' ', '{', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string className = "";
                                if (arrstr.Length > 1)
                                {
                                    {
                                        className = arrstr[1];
                                    }
                                    DBG("{0:0000} NS: {1}", lineNum, className);
                                    if (!ClassDict.ContainsKey(className))
                                    {
                                        ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                    }
                                    continue;
                                }
                            }
                        }
                        if (line.StartsWith("BEGIN_SHADER_PARAMETER_STRUCT")) //struct define in macro
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            if (!line.Contains(";") && !line.Contains("\\")) //(;)内部用, (\)在define中
                            {
                                string[] arrstr = line.Split(new char[] { ' ', '{', ',', ')', '(', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string className = "";
                                if (arrstr.Length > 1)
                                {
                                    {
                                        className = arrstr[1];
                                    }
                                    DBG("{0:0000} NS: {1}", lineNum, className);
                                    if (!ClassDict.ContainsKey(className))
                                    {
                                        ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                    }
                                    continue;
                                }
                            }
                        }

                        if (line.Contains("class"))
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);
                            if (!line.Contains(";") && !line.Contains(")"))//前置声明
                            {
                                string[] arrstr = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string className = "";
                                if (arrstr.Length > 1)
                                {
                                    if ((arrstr[0] == "class" && arrstr[1].Contains("_API"))
                                        || arrstr[1] == "class" && arrstr[0].Contains("_API"))
                                    {
                                        if (arrstr.Length > 2)
                                        {
                                            className = arrstr[2];
                                        }
                                    }
                                    else if (arrstr[0] == "class")
                                    {
                                        className = arrstr[1];
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    DBG("{0:0000} CL: {1}", lineNum, className);
                                    if (!ClassDict.ContainsKey(className))
                                    {
                                        ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                    }
                                    continue;
                                }
                            }
                        }

                        if (line.Contains("extern")) //function or global variable
                        {
                            DBG("{0:0000} CO: {1}", lineNum, line);

                            string[] arrstr = line.Split(new char[] { ' ', '{', '}', ',', ')', '(', ';' , '*', '&', '\t' }, StringSplitOptions.RemoveEmptyEntries); //'<', '>', no this because: template<int32>
                            string className = "";
                            if (arrstr.Length > 2)
                            {
                                //extern template class TRenderAssetUpdate<FTexture2DUpdateContext>;
                                //extern ENGINE_API void MyFunctionOrVariable
                                //ENGINE_API extern void MyFunctionOrVariable
                                //extern void MyFunctionOrVariable
                                //extern const FString Export;
                                //extern ENGINE_API class UEngine*			GEngine;
                                string[] keywords = { "extern", "class", "const", "template" };

                                int typePos = 0;
                                for (int i = 0; i < arrstr.Length; i++)
                                {
                                    bool NotKeyword = false;
                                    for (int j = 0; j < keywords.Length; j++)
                                    {
                                        if (arrstr[i] == keywords[j] || arrstr[i].Contains("_API"))
                                        {
                                            break;
                                        }
                                        else if(j == keywords.Length-1)
                                        {
                                            NotKeyword = true;
                                        }
                                    }
                                    if (NotKeyword)
                                    {
                                        typePos = i;
                                        break;
                                    }
                                }
                                if (typePos > 0 && typePos < arrstr.Length - 1)
                                    className = arrstr[(typePos+1)];

                                DBG("{0:0000} FG: {1}", lineNum, className);
                                if (!ClassDict.ContainsKey(className))
                                {
                                    ClassDict.Add(className, filePath + ":" + lineNum.ToString());
                                }
                                continue;
                            }
                        }                            
                        DBG("{0:0000} CO: {1}", lineNum, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error：{ex.Message}");
            }
        }

        static void procFile(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            if (ext is null)
                return;
            ext = ext.ToLower();

            if (ext == ".h" || ext == ".cpp" || ext == ".inl")
            {
                FindRefInFile(filePath);
            }
        }

        //3. 遍历模块B，对每个文件调用FindRefInFile()
        static void FindRefInDirectory(string fileDir, bool bRecursive)
        {
            if (Directory.Exists(fileDir))
            {
                string[] filePaths = Directory.GetFiles(fileDir);
                foreach (string filePath in filePaths)
                {
                    procFile(filePath);
                }

                if (bRecursive)
                {
                    // Recurse sub directories
                    string[] folders = Directory.GetDirectories(fileDir);
                    foreach (string folder in folders)
                    {
                        FindRefInDirectory(folder, bRecursive);
                    }
                }
            }
            else if (File.Exists(fileDir))
            {
                procFile(fileDir);
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run)
                .WithNotParsed(HandleParseError);
            if (bDebug)
            {
                Console.ReadLine();
            }
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            
        }
        static void Run(Options option)
        { 
            
            //string[] skipMacro = { "WITH_EDITOR", "0", "LOGTRACE_ENABLED", "WITH_EDITOR_ONLY_DATA", "UE_TRACE_ENABLED", "!UE_BUILD_SHIPPING", "VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = {"VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = { "0"};

            exclusiveDirs = option.exclusiveDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < exclusiveDirs.Length; i++)
            {
                exclusiveDirs[i] = exclusiveDirs[i].ToLower();
            }

            moduleDirs = option.moduleDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            refModuleDirs = option.refModuleDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            string[] arrSkipMacros = option.skipMacroString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in arrSkipMacros)
                skipMacros.Add(item);

            bShowInternalClass = option.bShowInternalClass;

            bDebug = option.bDebug;
            bVerbos = option.bVerbos;
            //1. 找到模块A中所有的.h文件
            for (int i = 0; i < moduleDirs.Length; i++)
            {
                FindAllHeaderInDirectory(moduleDirs[i], true);
            }
            //2. 找到模块A中.h文件的class定义
            foreach (KeyValuePair<string, string> kv in RefHeaderDict)
            {
                string filePath = kv.Value;
                FindClassesInHeaderFile(filePath);
            }
            //string Key = "FLightShaderParameters";
            //if (ClassDict.ContainsKey(Key))
            //{
            //    int breakhere = 1;
            //    Console.WriteLine("{0} defined in {1}.", Key, ClassDict[Key]);
            //}
            //3. 到模块B下，查看每个文件是否包含模块A中定义的任何一个class
            for (int i = 0; i < refModuleDirs.Length; i++)
            {
                FindRefInDirectory(refModuleDirs[i], true);
            }
            if (bDebug)
            {
                foreach (KeyValuePair<string, List<string>> kv in ClassRefDict)
                {
                    Console.WriteLine("{0} defined in {1} is referenced by:", kv.Key, ClassDict[kv.Key]);
                    foreach (string refItem in kv.Value)
                    {
                        Console.WriteLine("{0}", refItem);
                    }
                    Console.WriteLine("-----------------");
                }
                Console.WriteLine("------sumarry:-----------");
                Console.WriteLine("totalClasses: {0}, totalRefClass:{1}", ClassDict.Count(), ClassRefDict.Count());

                Console.WriteLine("------ref Classes:-----------");
            }
            //
            foreach (KeyValuePair<string, List<string>> kv in ClassRefDict)
            {
                string refString = "";
                int i = 1;
                foreach (string refItem in kv.Value)
                {
                    refString += (i.ToString()+" " + refItem + "$");
                    i++;
                }

                refString = refString.Remove(refString.Length - 1);
                Console.WriteLine("{0}#{1}#{2}#{3}", kv.Key, ClassDict[kv.Key], kv.Value.Count(), refString);
            }
            //if -i
            
            if (bShowInternalClass)
            {
                Console.WriteLine("===================internal classes =====================");
                int i = 0;
                foreach (KeyValuePair<string, string> kv in ClassDict)
                {
                    if (!ClassRefDict.ContainsKey(kv.Key))
                    {
                        i++;
                        Console.WriteLine("{0}#{1}#{2}", i, kv.Key, kv.Value);
                    }
                }
            }
        }
    }
}
