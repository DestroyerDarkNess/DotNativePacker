using dnlib.DotNet;
using dnlib.PE;
using Ressy;
using Ressy.HighLevel.Icons;
using Ressy.HighLevel.Versions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NativeV2_Packer
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Native Packer V2.0 By Destroyer | https://github.com/DestroyerDarkNess";
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Native Packer V2.0 For .NET Framework Assemblys" + Environment.NewLine);
            Console.WriteLine("Power By Destroyer | https://github.com/DestroyerDarkNess | Discord: Destroyer#8328" + Environment.NewLine);
            Console.WriteLine("Created for demonstration purposes only." + Environment.NewLine);
            Console.ForegroundColor = ConsoleColor.White;

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: NativeV2_Packer.exe <path_to_file.exe>");
            }
            else
            {
                string exePath = args[0];
                if (File.Exists(exePath))
                {
                    PackFile(exePath);
                }
                else
                {
                    Console.WriteLine("Remember Usage: NativeV2_Packer.exe <path_to_file.exe>");
                    Console.WriteLine("File not found.");
                }
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(Environment.NewLine + "Press any key to exit..." + Environment.NewLine);
            Console.ReadLine();
        }

        private static void PackFile(string FileToPack)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("//////////////////////// [--WARNING--] \\\\\\\\\\\\\\\\\\\\\\\\");
            Console.WriteLine("After Packaging, your Executable must be Protected with some Native PE Protection Tools");
            Console.WriteLine("For Example: VMP or Themida.");
            Console.WriteLine("Notes:" + Environment.NewLine);
            Console.WriteLine("  -- To avoid dumping, I implemented my AntiDump > https://github.com/DestroyerDarkNess/ExtremeAntidump");
            Console.WriteLine("  -- CommandLines are not supported as usual, You will need to extract the CommandLines using WMI (Basically Getting the Current Process information.)" + Environment.NewLine);
            Console.WriteLine(Environment.NewLine);
            Console.ForegroundColor = ConsoleColor.White;

            try
            {
                ModuleDefMD module = ModuleDefMD.Load(FileToPack);
                string Ouput = Path.Combine(Path.GetDirectoryName(FileToPack), Path.GetFileNameWithoutExtension(FileToPack) + "_Packed.exe");

                Console.WriteLine($"Assembly: {module.Name}");
                Console.WriteLine($"Entry Point: {module.EntryPoint}");
                Console.WriteLine($".NET Runtime: {module.RuntimeVersion}");

                var machineType = module.Metadata.PEImage.ImageNTHeaders.FileHeader.Machine;
                bool Isx64 = machineType == Machine.AMD64 || machineType == Machine.IA64;
                Console.WriteLine($"Is 64-bit: {Isx64}");

                string Compiler = Get_C_Compiler(Isx64);

                Console.WriteLine($"Tiny C Compiler: {Compiler}");

                // Generate shellcode from the .NET assembly
                byte[] shellcodeBytes = AssemblyToShellCode(FileToPack, module.EntryPoint);

                if (shellcodeBytes == null) { throw new Exception("Shellcode generation failed!"); }

                string C_Code = Generate_C_Loader();

                //Write C Loader Code
                string StubTempFile = Path.Combine(Path.GetTempPath(), "Temp.c");
                File.WriteAllText(StubTempFile, C_Code);

                //Make Compiler arguments
                string tccArguments = $"\"{StubTempFile}\" -o \"{Ouput}\" -luser32 -lkernel32 -mwindows";
                if (Ouput.ToLower().EndsWith(".dll")) tccArguments += " -shared";

                //Compile C Loader Code
                string tccResult = RunRemoteHost(Compiler, tccArguments);
                if (File.Exists(StubTempFile) == true) { File.Delete(StubTempFile); }

                if (string.IsNullOrEmpty(tccResult) == false) { tccResult = "Successful compilation."; }
                Console.WriteLine("Compiler Result: " + tccResult);

                //Inject shellcode into resource section of the compiled C Loader
                var portableExecutable = new PortableExecutable(Ouput);
                ResourceIdentifier RI = new ResourceIdentifier(Ressy.ResourceType.FromCode(10), ResourceName.FromCode(1));
                portableExecutable.SetResource(RI, shellcodeBytes);

                // Change Exe Info  
                try
                {
                    var version = module.Assembly.Version;
                    string description = GetCustomAttribute(module, "AssemblyDescription");
                    string company = GetCustomAttribute(module, "AssemblyCompany");
                    string product = GetCustomAttribute(module, "AssemblyProduct");

                    var versionInfo = new VersionInfoBuilder()
                        .SetFileVersion(version)
                        .SetProductVersion(version)
                        .SetFileType(FileType.Application)
                        .SetAttribute(VersionAttributeName.FileDescription, description)
                        .SetAttribute(VersionAttributeName.CompanyName, company)
                        .SetAttribute(VersionAttributeName.ProductName, product)
                        .Build();

                    portableExecutable.SetVersionInfo(versionInfo);
                }
                catch (Exception excep)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Failed to change exe info: {excep.Message}");
                }

                // Change Exe Icon
                try
                {
                    Icon AssemblyIcon = Icon.ExtractAssociatedIcon(FileToPack); // Generate Icon Bug/Glitch , Change Extract Icon Function for better
                    using (var iconStream = new MemoryStream())
                    {
                        AssemblyIcon.Save(iconStream);
                        iconStream.Position = 0;

                        portableExecutable.RemoveIcon();
                        portableExecutable.SetIcon(iconStream);
                    }
                }
                catch (Exception excep)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Failed to change exe icon: {excep.Message}");
                }


                if (File.Exists(Ouput))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Packed assembly saved to: {Ouput}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to save packed assembly to: {Ouput}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
            }
        }


        private static string Get_C_Compiler(bool IsX64)
        {
            try
            {
                string Tcc_Compiler = UnzipTCC_Compiler(Application.ExecutablePath);
                string tccX64 = System.IO.Path.Combine(Path.GetDirectoryName(Tcc_Compiler), "x86_64-win32-tcc.exe");

                if (IsX64) Tcc_Compiler = tccX64;

                return Tcc_Compiler;
            }
            catch { throw new Exception("Error Extracting Compiler, Please disable your antivirus."); }
        }

        private static string Generate_C_Loader()
        {
            StringBuilder cCodeBuilder = new StringBuilder();
            cCodeBuilder.AppendLine("#include <stdio.h>");
            cCodeBuilder.AppendLine("#include <stdlib.h>");
            cCodeBuilder.AppendLine("#include <windows.h>");
            cCodeBuilder.AppendLine();
            cCodeBuilder.AppendLine("void* loadEmbeddedResource(int resourceId, DWORD* size) {");
            cCodeBuilder.AppendLine("    HRSRC hResource = FindResource(NULL, MAKEINTRESOURCE(resourceId), RT_RCDATA);");
            cCodeBuilder.AppendLine("    if (hResource == NULL) { return NULL; }");
            cCodeBuilder.AppendLine("    HGLOBAL hMemory = LoadResource(NULL, hResource);");
            cCodeBuilder.AppendLine("    if (hMemory == NULL) { return NULL; }");
            cCodeBuilder.AppendLine("    *size = SizeofResource(NULL, hResource);");
            cCodeBuilder.AppendLine("    return LockResource(hMemory);");
            cCodeBuilder.AppendLine("}");
            cCodeBuilder.AppendLine();
            cCodeBuilder.AppendLine("int main() {");
            cCodeBuilder.AppendLine("    DWORD shellcodeSize;");
            cCodeBuilder.AppendLine("    unsigned char* shellcode = (unsigned char*)loadEmbeddedResource(1, &shellcodeSize);");
            cCodeBuilder.AppendLine("    if (shellcode == NULL) {");
            cCodeBuilder.AppendLine("        fprintf(stderr, \"Failed to load embedded resource.\\n\");");
            cCodeBuilder.AppendLine("        return 1;");
            cCodeBuilder.AppendLine("    }");
            cCodeBuilder.AppendLine();
            cCodeBuilder.AppendLine("    void* exec = VirtualAlloc(0, shellcodeSize, MEM_COMMIT, PAGE_EXECUTE_READWRITE);");
            cCodeBuilder.AppendLine("    if (exec == NULL) {");
            cCodeBuilder.AppendLine("        fprintf(stderr, \"VirtualAlloc failed.\\n\");");
            cCodeBuilder.AppendLine("        return 1;");
            cCodeBuilder.AppendLine("    }");
            cCodeBuilder.AppendLine();
            cCodeBuilder.AppendLine("    memcpy(exec, shellcode, shellcodeSize);");
            cCodeBuilder.AppendLine("    ((void(*)())exec)();");
            cCodeBuilder.AppendLine("    return 0;");
            cCodeBuilder.AppendLine("}");
            cCodeBuilder.AppendLine();

            return cCodeBuilder.ToString();
        }

        #region " Helper Functions  "

        private static string UnzipTCC_Compiler(string DirToExtract)
        {

            bool ExtractTCC = false;

            string CCompilerDir = Path.Combine(Path.GetDirectoryName(DirToExtract), "TCC");

            if (Directory.Exists(CCompilerDir) == false) { Directory.CreateDirectory(CCompilerDir); ExtractTCC = true; }

            string CCompilerExe = Path.Combine(CCompilerDir, "tcc.exe");

            if (File.Exists(CCompilerExe) == false) { ExtractTCC = true; }

            if (ExtractTCC == true)
            {
                string TempWriteZip = Path.Combine(Path.GetTempPath(), "Tcc.zip");

                if (File.Exists(TempWriteZip) == true) { File.Delete(TempWriteZip); }

                File.WriteAllBytes(TempWriteZip, Properties.Resources.Bin);

                System.IO.Compression.ZipFile.ExtractToDirectory(TempWriteZip, CCompilerDir);
            }

            return CCompilerExe;

        }

        public static byte[] AssemblyToShellCode(string TargetAssembly, MethodDef EntryPoint, string appdomainName = "")
        {
            string Donut = Path.Combine(Path.GetTempPath(), "donut.exe");

            if (!File.Exists(Donut)) File.WriteAllBytes(Donut, Properties.Resources.donut);

            string TempShell = Path.Combine(Path.GetTempPath(), "loader.b64");
            string TargetAsmName = Path.Combine(Path.GetTempPath(), "tempASMShell.exe");

            if (File.Exists(TargetAsmName)) File.Delete(TargetAsmName);

            File.Copy(TargetAssembly, TargetAsmName);

            System.Threading.Thread.Sleep(100);

            TypeDef declaringType = EntryPoint.DeclaringType;

            string FullDonutArgs = $"-f 2 -c {declaringType.Namespace + "." + declaringType.Name} -m {EntryPoint.Name} --input:{TargetAsmName}";

            if (appdomainName != "")
            {
                FullDonutArgs += " -d " + appdomainName;
            }

            string DonutResult = RunRemoteHost(Donut, FullDonutArgs);
            Console.WriteLine("Shell Output: " + DonutResult.Replace(TargetAsmName, "******").Replace("Donut", "Hydra").Replace("(built Mar  3 2023 13:33:22)", "").Replace("[ Copyright (c) 2019-2021 TheWover, Odzhan", "[ Github: https://github.com/DestroyerDarkNess"));

            if (File.Exists(TempShell) == true)
            {
                string data = File.ReadAllText(TempShell);

                if (File.Exists(TempShell)) File.Delete(TempShell);

                return Convert.FromBase64String(data);
            }
            else
            {
                return null;
            }

        }

        public static string RunRemoteHost(string Target, string FullArguments = "", bool redirectouput = true)
        {
            try
            {

                Process cmdProcess = new Process();
                {
                    var withBlock = cmdProcess;
                    withBlock.StartInfo = new ProcessStartInfo(Target, FullArguments);
                    {
                        var withBlock1 = withBlock.StartInfo;
                        if (redirectouput)
                        {
                            withBlock1.CreateNoWindow = true;
                            withBlock1.UseShellExecute = false;
                            withBlock1.RedirectStandardOutput = true;
                            withBlock1.RedirectStandardError = true;
                        }
                        withBlock1.WindowStyle = ProcessWindowStyle.Hidden;
                        withBlock1.WorkingDirectory = Path.GetDirectoryName(Target);
                    }
                    withBlock.Start();
                    withBlock.WaitForExit();
                }

                if (redirectouput)
                {
                    string HostOutput = cmdProcess.StandardOutput.ReadToEnd().ToString() + Environment.NewLine + cmdProcess.StandardError.ReadToEnd().ToString();
                    return HostOutput.ToString();
                }
                else { return ""; }

            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string GetCustomAttribute(ModuleDefMD module, string attributeName)
        {
            foreach (var attribute in module.Assembly.CustomAttributes)
            {
                if (attribute.TypeFullName.Contains(attributeName))
                {
                    return attribute.ConstructorArguments[0].Value.ToString();
                }
            }
            return null;
        }

        #endregion

    }
}
