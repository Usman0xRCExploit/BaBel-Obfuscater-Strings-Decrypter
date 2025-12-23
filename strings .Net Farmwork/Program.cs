using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Reflection;

class BabelStringDecryptor
{
    static void Main()
    {

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Drag EXE/DLL: ");
        Console.ForegroundColor = ConsoleColor.Green;
        string path = Console.ReadLine().Trim('\"');

        ModuleDefMD module = ModuleDefMD.Load(path);
        Assembly asm = Assembly.LoadFrom(path);

        int patchedStrings = 0;

        foreach (var type in module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                var instrs = method.Body.Instructions;

                for (int i = 0; i < instrs.Count - 3; i++)
                {

                    if (instrs[i].OpCode == OpCodes.Ldstr && instrs[i + 1].IsLdcI4() && instrs[i + 2].OpCode == OpCodes.Call)
                    {
                        if (instrs[i + 2].Operand is MethodDef DecrypterMethod)
                        {
                            if (!DecrypterMethod.HasBody) continue;
                            if (!DecrypterMethod.HasReturnType) continue;
                            if (DecrypterMethod.Parameters.Count == 2 &&
                                DecrypterMethod.Parameters[0].Type.FullName == "System.String" &&
                                DecrypterMethod.Parameters[1].Type.FullName == "System.Int32")
                            {

                                var Method = asm.ManifestModule.ResolveMethod(DecrypterMethod.MDToken.ToInt32());

                                object result = Method.Invoke(null, new object[] { instrs[i].Operand.ToString(), instrs[i + 1].GetLdcI4Value() });

                                if (result is string decryptedString)
                                {
                                    instrs[i].Operand = decryptedString;
                                    instrs[i + 1].OpCode = OpCodes.Nop;
                                    instrs[i + 2].OpCode = OpCodes.Nop;
                                    Console.WriteLine($"{instrs[i + 1].Operand} => {decryptedString}");
                                    patchedStrings++;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (patchedStrings > 0)
        {

            var options = new ModuleWriterOptions(module)
            {
                MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack | MetadataFlags.PreserveAll },
                Logger = DummyLogger.NoThrowInstance
            };

            string outPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "-Decrypted" + Path.GetExtension(path));
            module.Write(outPath, options);
            Console.WriteLine($"\n[+] Done! {patchedStrings} strings decrypted and patched.");
            Console.WriteLine($"[+] Saved to: {outPath}");
            Console.ReadKey();
        }
        else
        {
            Console.WriteLine("\n[-] No strings decrypted.");
            Console.ReadKey();
        }
    }

}