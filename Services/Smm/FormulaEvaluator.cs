using System.Data;
using System.Globalization;

namespace SW.PC.API.Backend.Services.Smm;

/// <summary>
/// Evaluador de fórmulas aritméticas para SMM (DEC-016/DEC-021).
/// Sustituye a NCalc — usa <see cref="DataTable.Compute"/>, incluido en
/// .NET 8 sin paquetes externos ni dependencias transitivas problemáticas.
///
/// Sintaxis soportada: + - * / % ( ) , operadores lógicos AND/OR/NOT,
/// comparaciones &lt; &gt; &lt;= &gt;= = &lt;&gt;, funciones IIF, ABS, LEN, TRIM,
/// ISNULL. Suficiente para fórmulas tipo "({A}-{B})/{C}" o "IIF({A}&gt;0,{B}/{A},0)".
///
/// La sustitución de placeholders {VarName} se hace ANTES de llamar a Evaluate,
/// por lo que el evaluador recibe siempre una expresión con literales numéricos
/// en cultura invariante (separador decimal ".").
/// </summary>
internal static class FormulaEvaluator
{
    private static readonly DataTable _table = new() { Locale = CultureInfo.InvariantCulture };

    /// <summary>
    /// Evalúa una expresión aritmética y devuelve el resultado como object.
    /// Lanza <see cref="EvaluateException"/> o <see cref="SyntaxErrorException"/>
    /// si la expresión es inválida; el llamador ya las captura como FormulaError.
    /// </summary>
    public static object? Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        // DataTable.Compute no es thread-safe; serializamos accesos.
        lock (_table)
        {
            var result = _table.Compute(expression, null);
            return result is DBNull ? null : result;
        }
    }
}
