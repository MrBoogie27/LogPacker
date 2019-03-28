using System;
using System.Collections.Generic;
using System.Text;

namespace Kontur.LogPacker
{
    interface ILogCompressor
    {
        /// <summary>
        /// Метод Compress возвращает путь на файл сжатого лога
        /// </summary>
        /// <param name="inputFile">Путь на исходный файл</param>
        /// <param name="temporaryFile">Путь на временный файл</param>
        void Compress(string inputFile, string temporaryFile);

        /// <summary>
        /// Метод Decompress производит разжатие файла
        /// </summary>
        /// <param name="inputFile">Путь на исходный файл</param>
        /// <param name="outputFile">Путь на итоговый файл</param>
        void Decompress(string inputFile, string outputFile);
    }
}
