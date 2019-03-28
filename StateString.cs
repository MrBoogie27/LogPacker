using System;
using System.Collections.Generic;
using System.Text;

namespace Kontur.LogPacker
{
    /// <summary>
    /// Состояние считывания строки
    /// </summary>
    enum StateString
    {
        ///<summary>Строка занимает весь буфер; не включает перевода строки LF; нет таких же предшедствующих строк</summary>
        FirstFullString,
        ///<summary>Строка занимает весь буфер; не включает перевода строки LF; есть такие же предшедствующие строки</summary>
        NotFirstFullString,
        ///<summary>Строка не занимает весь буфер; включает перевод строки LF</summary>
        GoodString,
        ///<summary>Строка не занимает весь буфер; не включает перевод строки LF</summary>
        UnreadString,
        ///<summary>Строка является концом большой строки, которая не влезла в буфер; включает перевод строки LF.</summary>
        UselessString
    }
}
