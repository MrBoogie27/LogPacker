using System;
using System.IO;

namespace Kontur.LogPacker
{
    internal static class EntryPoint
    {
        public static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                var (inputFile, outputFile) = (args[0], args[1]);

                if (File.Exists(inputFile))
                {  
                    Compress(inputFile, outputFile);
                    return;
                }
            }

            if (args.Length == 3 && args[0] == "-d")
            {
                var (inputFile, outputFile) = (args[1], args[2]);

                if (File.Exists(inputFile))
                {
                    Decompress(inputFile, outputFile);
                    return;
                }
            }

            ShowUsage();
        }
        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} [-d] <inputFile> <outputFile>");
            Console.WriteLine("\t-d flag turns on the decompression mode");
            Console.WriteLine();
        }

        private static void Compress(string inputFile, string outputFile)
        {
            /// <summary>
            /// Имя временного файла
            /// </summary>
            string temporaryFile = inputFile.Remove(0, inputFile.LastIndexOf('\\') + 1) + ".compressed";

            //Сжимаем исходный файл во вспомогательный файл
            new LogCompressor().Compress(inputFile, temporaryFile);

            //Сжимаем вспомогательный файл
            using (var temporaryStream = File.OpenRead(temporaryFile))
            using (var outputStream = File.OpenWrite(outputFile))
                new GZipCompressor().Compress(temporaryStream, outputStream);

            //Удаляем вспомогателньый файл
            new FileInfo(temporaryFile).Delete();
        }

        private static void Decompress(string inputFile, string outputFile)
        {
            /// <summary>
            /// Имя временного файла
            /// </summary>
            string temporaryFile = inputFile.Remove(0, inputFile.LastIndexOf('\\') + 1) + ".compressed";

            //Восстанавливаем вспомогателньый файл
            using (var inputStream = File.OpenRead(inputFile))
            using (var temporaryStream = File.OpenWrite(temporaryFile))
                new GZipCompressor().Decompress(inputStream, temporaryStream);

            //Вспомогательный файл преобразуем в исходный
            new LogCompressor().Decompress(temporaryFile, outputFile);

            //Удаляем вспомогателньый файл
            new FileInfo(temporaryFile).Delete();
        }
    }
}