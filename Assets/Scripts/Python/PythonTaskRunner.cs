using System;
using System.Collections.Generic;
using MortgageSim.Tasks;

namespace MortgageSim.Python
{
    /// <summary>
    /// Прогоняет решение игрока по скрытым тестам задачи.
    /// Пока используется встроенный C#-интерпретатор; позже сюда можно
    /// подставить настоящий Python, не меняя интерфейс задач.
    /// </summary>
    public static class PythonTaskRunner
    {
        public static PythonRunResult Run(PythonTask task, string code)
        {
            var result = new PythonRunResult();
            if (task == null)
            {
                result.Error = "Задача не найдена";
                return result;
            }

            // Сначала проверяем синтаксис и верхнеуровневый код.
            try
            {
                var probe = new Interpreter();
                probe.Execute(code);
                if (!(probe.GetGlobal(task.FunctionName) is PyFunction))
                {
                    result.Error = "Не найдена функция " + task.FunctionName + "(). Проверьте имя и отступы.";
                    return result;
                }
                result.ConsoleOutput = probe.Output.ToString();
            }
            catch (PythonSyntaxException ex)
            {
                result.Error = "Синтаксис, строка " + ex.Line + ": " + ex.Message;
                return result;
            }
            catch (PythonRuntimeException ex)
            {
                result.Error = "Ошибка, строка " + ex.Line + ": " + ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = "Ошибка выполнения: " + ex.Message;
                return result;
            }

            foreach (var test in task.Tests)
            {
                var outcome = new PythonTestOutcome { Label = test.Label, Expected = PyFormat.Repr(test.Expected) };
                try
                {
                    var interpreter = new Interpreter();
                    interpreter.Execute(code);
                    object function = interpreter.GetGlobal(task.FunctionName);
                    var args = new List<object>();
                    foreach (var arg in test.Args) args.Add(ToPy(arg));
                    object actual = interpreter.CallFunction(function, args);
                    outcome.Actual = PyFormat.Repr(actual);
                    outcome.Passed = Interpreter.Equal(actual, ToPy(test.Expected));
                }
                catch (PythonSyntaxException ex)
                {
                    outcome.Error = "синтаксис, строка " + ex.Line + ": " + ex.Message;
                }
                catch (PythonRuntimeException ex)
                {
                    outcome.Error = "строка " + ex.Line + ": " + ex.Message;
                }
                catch (Exception ex)
                {
                    outcome.Error = ex.Message;
                }

                result.Outcomes.Add(outcome);
            }

            result.Success = result.Outcomes.Count > 0 && result.PassedCount == result.Outcomes.Count;
            return result;
        }

        private static object ToPy(object value)
        {
            if (value == null) return null;
            if (value is int i) return (long)i;
            if (value is long || value is double || value is string || value is bool) return value;
            if (value is List<object> list)
            {
                var converted = new List<object>();
                foreach (var item in list) converted.Add(ToPy(item));
                return converted;
            }
            if (value is Dictionary<string, object> dict)
            {
                var converted = new Dictionary<string, object>();
                foreach (var pair in dict) converted[pair.Key] = ToPy(pair.Value);
                return converted;
            }
            if (value is object[] array)
            {
                var converted = new List<object>();
                foreach (var item in array) converted.Add(ToPy(item));
                return converted;
            }
            return value;
        }
    }
}
