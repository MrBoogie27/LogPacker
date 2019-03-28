using System;
using System.IO;
using System.Text;

namespace Kontur.LogPacker
{
    internal class LogCompressor: ILogCompressor
    {
        /// <summary>
        /// Дата начала отсчёта
        /// </summary>
        readonly DateTime startDate = new DateTime(1, 1, 1, 1, 1, 1, 1);

        /// <summary>
        /// Размер буффера для считывания
        /// </summary>
        const int bufferSize = 1024;

        /// <summary>
        /// Количество слов для поиска
        /// </summary>
        const int numberWords = 4;

        public void Compress(string inputFile, string temporaryFile)
        {
            using (var temporaryStream = File.Create(temporaryFile))
            using (var inputStream = File.OpenRead(inputFile))
            {
                #region Объявление пременных

                /// <summary>
                /// Состояние автомата при считывание
                /// </summary>
                StateString state = StateString.GoodString;

                /// <summary>
                /// Главный буффер для считывания
                /// </summary>
                byte[] mainBuffer = new byte[bufferSize];

                /// <summary>
                /// Вспомогательный буффер для одной строки
                /// </summary>
                byte[] stringBuffer = new byte[bufferSize];

                /// <summary>
                /// Старая дата
                /// </summary>
                DateTime oldDate = startDate;

                /// <summary>
                /// Старое число из полуинтервала [0, ulong.MaxValue)
                /// </summary>
                ulong oldNumber = 0;

                /// <summary>
                /// Размеры буфферов
                /// </summary>
                int mbSize = 0, sbSize = 0;

                /// <summary>
                /// Массив индексов указыващие на начало слова
                /// </summary>
                int[] posWordStart = new int[numberWords];

                /// <summary>
                /// Массив индексов указыващие на конец слова
                /// </summary>
                int[] posWordEnd = new int[numberWords];

                /// <summary>
                /// Резмеры массивов индексов
                /// </summary>
                int pwsSize = 0, pweSize = 0;
                #endregion

                mbSize = inputStream.Read(mainBuffer, 0, bufferSize);
                while (mbSize != 0)
                {
                    /// <summary>
                    /// Итератор главного буффера
                    /// </summary>
                    int i = -1;

                    while (i < mbSize - 1)
                    {
                        i++;
                        for (; i < mbSize && sbSize < bufferSize; i++, sbSize++)
                        {
                            stringBuffer[sbSize] = mainBuffer[i];
                            
                            //Отсекаем строки по символу LF
                            if (mainBuffer[i] == 0x0A)
                            {
                                sbSize++;
                                break;
                            }

                            //Ищем позиции начала слова - если непробельному символу предшествует пробельный 
                            if (sbSize != 0 && stringBuffer[sbSize] != 0x20 && stringBuffer[sbSize - 1] == 0x20 && pwsSize != numberWords)
                            {
                                posWordStart[pwsSize] = sbSize;
                                pwsSize++;
                            }

                            //Ищем позиции конца слова - если пробельному символу предшествует непробельный
                            if (sbSize != 0 && stringBuffer[sbSize] == 0x20 && stringBuffer[sbSize - 1] != 0x20 && pweSize != numberWords)
                            {
                                posWordEnd[pweSize] = sbSize - 1;
                                pweSize++;
                            }
                        }

                        //Если вышли из цикла из-за заполнения вспомогательного буффера стоит уменьшить итератор главного буффера
                        if (sbSize == bufferSize)
                            i--;

                        //Выполняем переход между состояниями
                        switch (state)
                        {
                            case StateString.FirstFullString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.UselessString;
                                    else
                                        state = StateString.NotFirstFullString;
                                }
                                break;
                            case StateString.NotFirstFullString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.UselessString;
                                    else
                                        state = StateString.NotFirstFullString;
                                }
                                break;
                            case StateString.GoodString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else if (sbSize == bufferSize)
                                        state = StateString.FirstFullString;
                                    else
                                        state = StateString.UnreadString;
                                }
                                break;
                            case StateString.UnreadString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else
                                        state = StateString.FirstFullString;
                                }
                                break;
                            case StateString.UselessString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else
                                        state = StateString.UnreadString;
                                }
                                break;
                        }

                        //Производим запись в файл
                        if (state == StateString.FirstFullString || state == StateString.GoodString)
                        {
                            //Если количество слов достаточно
                            if (pwsSize > 2 && pweSize > 2)
                            {
                                //Получаем строку с датой
                                string dateString = Encoding.UTF8.GetString(stringBuffer, 0, posWordEnd[1] + 1).Replace(',', '.');

                                //Пытаемся перевести слова в дату и число
                                if (DateTime.TryParse(dateString, out DateTime date) && ulong.TryParse(Encoding.UTF8.GetString(stringBuffer, posWordStart[1], posWordEnd[2] - posWordStart[1] + 1), out ulong number))
                                {
                                    //Записываем разницу между двумя датами в миллисекундах
                                    temporaryStream.Write(Encoding.UTF8.GetBytes(date.Subtract(oldDate).TotalMilliseconds.ToString()));
                                    //0x20 - пробел
                                    temporaryStream.WriteByte(0x20);
                                    //Записываем разницу чисел
                                    temporaryStream.Write(Encoding.UTF8.GetBytes((number - oldNumber).ToString()));
                                    temporaryStream.WriteByte(0x20);
                                    oldDate = date;
                                    oldNumber = number;
                                    //Записываем оставшуюся строку
                                    temporaryStream.Write(stringBuffer, posWordStart[2], sbSize - posWordStart[2]);
                                }
                                else
                                {
                                    //0x2E - точка = строка не сжата
                                    temporaryStream.WriteByte(0x2E);
                                    temporaryStream.Write(stringBuffer, 0, sbSize);
                                }
                            }
                            else
                            {
                                //0x2E - точка = строка не сжата
                                temporaryStream.WriteByte(0x2E);
                                temporaryStream.Write(stringBuffer, 0, sbSize);
                            }
                            //Т.к. запись произведена - обнуляем счётчики
                            pwsSize = pweSize = sbSize = 0;
                        } 
                        else if (state == StateString.NotFirstFullString || state == StateString.UselessString)
                        {
                            //Просто записываем строку, т.к. нет данных для сжатия
                            temporaryStream.Write(stringBuffer, 0, sbSize);
                            //Т.к. запись произведена - обнуляем счётчики
                            pwsSize = pweSize = sbSize = 0;
                        }
                    }
                    //Считываем новую строку
                    mbSize = inputStream.Read(mainBuffer, 0, bufferSize);
                }

                //Если по окончании считывания осталась необработанная строка во вспомогательном буффере 
                if (state == StateString.UnreadString)
                {
                    //Если количество слов достаточно
                    if (pwsSize > 2 && pweSize > 2)
                    {
                        //Получаем строку с датой
                        string dateString = Encoding.UTF8.GetString(stringBuffer, 0, posWordEnd[1] + 1).Replace(',', '.');

                        //Пытаемся перевести слова в дату и число
                        if (DateTime.TryParse(dateString, out DateTime date) && ulong.TryParse(Encoding.UTF8.GetString(stringBuffer, posWordStart[1], posWordEnd[2] - posWordStart[1] + 1), out ulong number))
                        {
                            //Записываем разницу между двумя датами в миллисекундах
                            temporaryStream.Write(Encoding.UTF8.GetBytes(date.Subtract(oldDate).TotalMilliseconds.ToString()));
                            //0x20 - пробел
                            temporaryStream.WriteByte(0x20);
                            //Записываем разницу чисел
                            temporaryStream.Write(Encoding.UTF8.GetBytes((number - oldNumber).ToString()));
                            temporaryStream.WriteByte(0x20);
                            oldDate = date;
                            oldNumber = number;
                            //Записываем оставшуюся строку
                            temporaryStream.Write(stringBuffer, posWordStart[2], sbSize - posWordStart[2]);
                        }
                        else
                        {
                            //0x2E - точка = строка не сжата
                            temporaryStream.WriteByte(0x2E);
                            temporaryStream.Write(stringBuffer, 0, sbSize);
                        }
                    }
                    else
                    {
                        //0x2E - точка = строка не сжата
                        temporaryStream.WriteByte(0x2E);
                        temporaryStream.Write(stringBuffer, 0, sbSize);
                    }
                }
            }
        }

        public void Decompress(string inputFile, string outputFile)
        {
            using (var temporaryStream = File.OpenWrite(outputFile))
            using (var inputStream = File.OpenRead(inputFile))
            {
                #region Объявление пременных

                /// <summary>
                /// Состояние автомата при считывание
                /// </summary>
                StateString state = StateString.GoodString;

                /// <summary>
                /// Главный буффер для считывания
                /// </summary>
                byte[] mainBuffer = new byte[bufferSize];

                /// <summary>
                /// Вспомогательный буффер для одной строки
                /// </summary>
                byte[] stringBuffer = new byte[bufferSize];

                /// <summary>
                /// Старая дата
                /// </summary>
                DateTime oldDate = startDate;

                /// <summary>
                /// Старое число из полуинтервала [0, ulong.MaxValue)
                /// </summary>
                ulong oldNumber = 0;

                /// <summary>
                /// Размеры буфферов
                /// </summary>
                int mbSize = 0, sbSize = 0;

                /// <summary>
                /// Массив индексов указыващие на начало слова
                /// </summary>
                int[] posWordStart = new int[numberWords];

                /// <summary>
                /// Массив индексов указыващие на конец слова
                /// </summary>
                int[] posWordEnd = new int[numberWords];

                /// <summary>
                /// Резмеры массивов индексов
                /// </summary>
                int pwsSize = 0, pweSize = 0;
                #endregion

                mbSize = inputStream.Read(mainBuffer, 0, bufferSize);

                while (mbSize != 0)
                {
                    /// <summary>
                    /// Итератор главного буффера
                    /// </summary>
                    int i = -1;

                    while (i < mbSize - 1)
                    {
                        i++;
                        for (; i < mbSize && sbSize < bufferSize; i++, sbSize++)
                        {
                            stringBuffer[sbSize] = mainBuffer[i];

                            //Отсекаем строки по символу LF
                            if (mainBuffer[i] == 0x0A)
                            {
                                sbSize++;
                                break;
                            }

                            //Ищем позиции начала слова - если непробельному символу предшествует пробельный 
                            if (sbSize != 0 && stringBuffer[sbSize] != 0x20 && stringBuffer[sbSize - 1] == 0x20 && pwsSize != numberWords)
                            {
                                posWordStart[pwsSize] = sbSize;
                                pwsSize++;
                            }

                            //Ищем позиции конца слова - если пробельному символу предшествует непробельный
                            if (sbSize != 0 && stringBuffer[sbSize] == 0x20 && stringBuffer[sbSize - 1] != 0x20 && pweSize != numberWords)
                            {
                                posWordEnd[pweSize] = sbSize - 1;
                                pweSize++;
                            }
                        }

                        //Если вышли из цикла из-за заполнения вспомогательного буффера стоит уменьшить итератор главного буффера
                        if (sbSize == bufferSize)
                            i--;

                        //Выполняем переход между состояниями
                        switch (state)
                        {
                            case StateString.FirstFullString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.UselessString;
                                    else
                                        state = StateString.NotFirstFullString;
                                }
                                break;
                            case StateString.NotFirstFullString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.UselessString;
                                    else
                                        state = StateString.NotFirstFullString;
                                }
                                break;
                            case StateString.GoodString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else if (sbSize == bufferSize)
                                        state = StateString.FirstFullString;
                                    else
                                        state = StateString.UnreadString;
                                }
                                break;
                            case StateString.UnreadString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else
                                        state = StateString.FirstFullString;
                                }
                                break;
                            case StateString.UselessString:
                                {
                                    if (stringBuffer[sbSize - 1] == 0x0A)
                                        state = StateString.GoodString;
                                    else
                                        state = StateString.UnreadString;
                                }
                                break;
                        }

                        //Производим запись в файл
                        if (state == StateString.FirstFullString || state == StateString.GoodString)
                        {
                            //Если в начале стоит 0x2E - точка = строка не сжата
                            if (stringBuffer[0] == 0x2E)
                            {
                                temporaryStream.Write(stringBuffer, 1, sbSize - 1);
                            }
                            else if (double.TryParse(Encoding.UTF8.GetString(stringBuffer, 0, posWordEnd[0] + 1), out double time) && 
                                     ulong.TryParse(Encoding.UTF8.GetString(stringBuffer, posWordStart[0], posWordEnd[1] - posWordStart[0] + 1), out ulong difference))
                            {
                                //Добавляем разницу между двумя датами в миллисекундах - time
                                DateTime date = oldDate.AddMilliseconds(time);
                                //Записываем дату
                                temporaryStream.Write(Encoding.UTF8.GetBytes(date.ToString("yyyy-MM-dd HH:mm:ss,fff")));
                                //0x20 - пробел
                                temporaryStream.WriteByte(0x20);
                                //Записываем новое число дополненное справа пробелами до 6 символов
                                temporaryStream.Write(Encoding.UTF8.GetBytes((difference + oldNumber).ToString().PadRight(6)));
                                temporaryStream.WriteByte(0x20);
                                oldDate = date;
                                oldNumber = difference + oldNumber;
                                //Записываем оставшуюся строку
                                temporaryStream.Write(stringBuffer, posWordStart[1], sbSize - posWordStart[1]);
                            }
                            //Т.к. запись произведена - обнуляем счётчики
                            pwsSize = pweSize = sbSize = 0;
                        }
                        else if (state == StateString.NotFirstFullString || state == StateString.UselessString)
                        {
                            temporaryStream.Write(stringBuffer, 0, sbSize);
                            //Т.к. запись произведена - обнуляем счётчики
                            pwsSize = pweSize = sbSize = 0;
                        }
                    }

                    //Считываем новую строку
                    mbSize = inputStream.Read(mainBuffer, 0, bufferSize);
                }

                //Если по окончании считывания осталась необработанная строка во вспомогательном буффере 
                if (state == StateString.UnreadString)
                {
                    if (stringBuffer[0] == 0x2E)
                    {
                        temporaryStream.Write(stringBuffer, 1, sbSize - 1);
                    }
                    else if (double.TryParse(Encoding.UTF8.GetString(stringBuffer, 0, posWordEnd[0] + 1), out double time) && ulong.TryParse(Encoding.UTF8.GetString(stringBuffer, posWordStart[0], posWordEnd[1] - posWordStart[0] + 1), out ulong number))
                    {
                        DateTime date = oldDate.AddMilliseconds(time);
                        temporaryStream.Write(Encoding.UTF8.GetBytes(date.ToString("yyyy-MM-dd HH:mm:ss,fff")));
                        temporaryStream.WriteByte(0x20);
                        temporaryStream.Write(Encoding.UTF8.GetBytes((number + oldNumber).ToString().PadRight(6)));
                        temporaryStream.WriteByte(0x20);
                        oldDate = date;
                        oldNumber = number + oldNumber;
                        temporaryStream.Write(stringBuffer, posWordStart[1], sbSize - posWordStart[1]);
                    }
                }
            }
        }
    }
}