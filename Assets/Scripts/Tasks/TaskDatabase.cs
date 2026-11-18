using System.Collections.Generic;

namespace MortgageSim.Tasks
{
    /// <summary>
    /// Все 12 задач. Задачи специально составлены так, чтобы их можно было
    /// проверить встроенным интерпретатором подмножества Python (без import).
    /// Позже этот слой легко заменить на настоящий Python.
    /// </summary>
    public static class TaskDatabase
    {
        public static List<PythonTask> CreateAll()
        {
            var tasks = new List<PythonTask>();

            tasks.Add(new PythonTask
            {
                Month = 1,
                Id = "task_01_hello",
                Title = "Первый заказ: приветствие",
                Difficulty = "Легко",
                FunctionName = "greet",
                Description =
                    "Заказчик просит функцию greet(name), которая возвращает приветствие.\n\n" +
                    "Верни строку: \"Привет, <имя>!\" — без лишних пробелов.\n" +
                    "Собери её через конкатенацию строк или f-строку.",
                Hint = "Верни \"Привет, \" + name + \"!\"",
                Examples = new[] { "greet(\"Аня\")  ->  \"Привет, Аня!\"", "greet(\"Мир\")  ->  \"Привет, Мир!\"" },
                StarterCode =
                    "def greet(name):\n" +
                    "    # Верни приветствие для name\n" +
                    "    return \"привет\"\n",
                Tests =
                {
                    new PythonTestCase("greet(\"Аня\")", "Привет, Аня!", "Аня"),
                    new PythonTestCase("greet(\"Мир\")", "Привет, Мир!", "Мир"),
                    new PythonTestCase("greet(\"коллега\")", "Привет, коллега!", "коллега"),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 2,
                Id = "task_02_sum",
                Title = "Сумма чисел от 1 до N",
                Difficulty = "Легко",
                FunctionName = "sum_to",
                Description =
                    "Заказчику нужен отчёт по выплатам. Напиши sum_to(n),\n" +
                    "которая возвращает сумму всех целых чисел от 1 до n включительно.\n\n" +
                    "Для n <= 0 верни 0. Используй цикл for и range.",
                Hint = "Заведи total = 0 и прибавляй i в цикле for i in range(1, n + 1).",
                Examples = new[] { "sum_to(5)  ->  15", "sum_to(0)  ->  0" },
                StarterCode =
                    "def sum_to(n):\n" +
                    "    total = 0\n" +
                    "    # Сложи числа от 1 до n\n" +
                    "    return total\n",
                Tests =
                {
                    new PythonTestCase("sum_to(0)", 0L, 0L),
                    new PythonTestCase("sum_to(1)", 1L, 1L),
                    new PythonTestCase("sum_to(5)", 15L, 5L),
                    new PythonTestCase("sum_to(100)", 5050L, 100L),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 3,
                Id = "task_03_vowels",
                Title = "Анализ текста: гласные",
                Difficulty = "Легко",
                FunctionName = "count_vowels",
                Description =
                    "Аналитический бот считает активность пользователей.\n" +
                    "Напиши count_vowels(text), которая возвращает количество гласных букв.\n\n" +
                    "Гласные: a e i o u y, а также русские а е ё и о у ы э ю я.\n" +
                    "Регистр не важен: \"А\" и \"а\" считаются одинаково.",
                Hint = "Приведи text к нижнему регистру: text = text.lower(). Потом проверяй if ch in vowels.",
                Examples = new[] { "count_vowels(\"привет\")  ->  2", "count_vowels(\"hello\")  ->  2" },
                StarterCode =
                    "def count_vowels(text):\n" +
                    "    vowels = \"aeiouyаеёиоуыэюя\"\n" +
                    "    count = 0\n" +
                    "    # Пройди по каждой букве\n" +
                    "    return count\n",
                Tests =
                {
                    new PythonTestCase("count_vowels(\"привет\")", 2L, "привет"),
                    new PythonTestCase("count_vowels(\"hello\")", 2L, "hello"),
                    new PythonTestCase("count_vowels(\"\")", 0L, ""),
                    new PythonTestCase("count_vowels(\"РИТМ\")", 1L, "РИТМ"),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 4,
                Id = "task_04_palindrome",
                Title = "Проверка палиндрома",
                Difficulty = "Легко",
                FunctionName = "is_palindrome",
                Description =
                    "Заказчик хочет проверять ники на симметричность.\n" +
                    "Напиши is_palindrome(text), которая возвращает True,\n" +
                    "если строка читается одинаково слева направо и справа налево.\n\n" +
                    "Регистр учитывается, пробелы не удаляются. Пустая строка — палиндром.",
                Hint = "Сравни text с перевёрнутой строкой: text[::-1]. Либо иди двумя индексами с концов.",
                Examples = new[] { "is_palindrome(\"level\")  ->  True", "is_palindrome(\"python\")  ->  False" },
                StarterCode =
                    "def is_palindrome(text):\n" +
                    "    # Сравни строку с её перевёрнутой версией\n" +
                    "    return False\n",
                Tests =
                {
                    new PythonTestCase("is_palindrome(\"level\")", true, "level"),
                    new PythonTestCase("is_palindrome(\"python\")", false, "python"),
                    new PythonTestCase("is_palindrome(\"\")", true, ""),
                    new PythonTestCase("is_palindrome(\"шалаш\")", true, "шалаш"),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 5,
                Id = "task_05_fizzbuzz",
                Title = "FizzBuzz для тимлида",
                Difficulty = "Средне",
                FunctionName = "fizzbuzz",
                Description =
                    "Классика собеседований. Напиши fizzbuzz(n), которая возвращает список строк\n" +
                    "для чисел от 1 до n включительно:\n\n" +
                    "  • число делится на 3 и на 5  ->  \"FizzBuzz\"\n" +
                    "  • только на 3               ->  \"Fizz\"\n" +
                    "  • только на 5               ->  \"Buzz\"\n" +
                    "  • иначе                     ->  строковое число, например \"7\"",
                Hint = "Сначала проверяй деление на 15 (или на 3 и 5 одновременно), иначе Fizz/Buzz перепутаются.",
                Examples = new[] { "fizzbuzz(5)  ->  [\"1\", \"2\", \"Fizz\", \"4\", \"Buzz\"]" },
                StarterCode =
                    "def fizzbuzz(n):\n" +
                    "    result = []\n" +
                    "    for i in range(1, n + 1):\n" +
                    "        # Добавь нужную строку через result.append(...)\n" +
                    "        pass\n" +
                    "    return result\n",
                Tests =
                {
                    new PythonTestCase("fizzbuzz(5)", NewList("1", "2", "Fizz", "4", "Buzz"), 5L),
                    new PythonTestCase("fizzbuzz(15)", NewList("1", "2", "Fizz", "4", "Buzz", "Fizz", "7", "8", "Fizz", "Buzz", "11", "Fizz", "13", "14", "FizzBuzz"), 15L),
                    new PythonTestCase("fizzbuzz(0)", NewList(), 0L),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 6,
                Id = "task_06_factorial",
                Title = "Факториал и рекурсия",
                Difficulty = "Средне",
                FunctionName = "factorial",
                Description =
                    "Математический модуль для отчётов. Напиши factorial(n),\n" +
                    "которая возвращает n! (произведение чисел от 1 до n).\n\n" +
                    "0! = 1. Для отрицательных чисел верни None.\n" +
                    "Можно решить рекурсией или циклом.",
                Hint = "Базовый случай: if n <= 1: return 1. Иначе return n * factorial(n - 1).",
                Examples = new[] { "factorial(0)  ->  1", "factorial(5)  ->  120" },
                StarterCode =
                    "def factorial(n):\n" +
                    "    # Обработай n <= 1 и отрицательные числа\n" +
                    "    return 0\n",
                Tests =
                {
                    new PythonTestCase("factorial(0)", 1L, 0L),
                    new PythonTestCase("factorial(1)", 1L, 1L),
                    new PythonTestCase("factorial(5)", 120L, 5L),
                    new PythonTestCase("factorial(10)", 3628800L, 10L),
                    new PythonTestCase("factorial(-3)", null, -3L),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 7,
                Id = "task_07_fib",
                Title = "Числа Фибоначчи",
                Difficulty = "Средне",
                FunctionName = "fib",
                Description =
                    "Финансовый бот считает рост вклада. Напиши fib(n),\n" +
                    "которая возвращает n-е число Фибоначчи.\n\n" +
                    "fib(0) = 0, fib(1) = 1, каждое следующее — сумма двух предыдущих.\n" +
                    "Постарайся обойтись без рекурсии, чтобы не ждать слишком долго.",
                Hint = "Держи два числа a = 0, b = 1 и сдвигай их в цикле. Либо используй список.",
                Examples = new[] { "fib(0)  ->  0", "fib(10)  ->  55" },
                StarterCode =
                    "def fib(n):\n" +
                    "    a = 0\n" +
                    "    b = 1\n" +
                    "    # Продвинься до n-го числа\n" +
                    "    return 0\n",
                Tests =
                {
                    new PythonTestCase("fib(0)", 0L, 0L),
                    new PythonTestCase("fib(1)", 1L, 1L),
                    new PythonTestCase("fib(10)", 55L, 10L),
                    new PythonTestCase("fib(15)", 610L, 15L),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 8,
                Id = "task_08_word_count",
                Title = "Частотный словарь",
                Difficulty = "Средне",
                FunctionName = "word_count",
                Description =
                    "Нужен анализатор отзывов. Напиши word_count(text),\n" +
                    "которая возвращает словарь: слово -> сколько раз оно встретилось.\n\n" +
                    "Слова разделены пробелами. Регистр не важен: приводи слова к нижнему регистру.\n" +
                    "Пустая строка даёт пустой словарь.",
                Hint = "words = text.lower().split(); затем counts[word] = counts.get(word, 0) + 1.",
                Examples = new[] { "word_count(\"кот пёс кот\")  ->  {\"кот\": 2, \"пёс\": 1}" },
                StarterCode =
                    "def word_count(text):\n" +
                    "    counts = {}\n" +
                    "    # Разбей текст на слова и посчитай их\n" +
                    "    return counts\n",
                Tests =
                {
                    new PythonTestCase("word_count(\"кот пёс кот\")", NewDict(("кот", 2L), ("пёс", 1L)), "кот пёс кот"),
                    new PythonTestCase("word_count(\"One two TWO\")", NewDict(("one", 1L), ("two", 2L)), "One two TWO"),
                    new PythonTestCase("word_count(\"\")", NewDict(), ""),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 9,
                Id = "task_09_second_largest",
                Title = "Второй по величине",
                Difficulty = "Сложнее",
                FunctionName = "second_largest",
                Description =
                    "Аналитика продаж. Напиши second_largest(nums), которая возвращает\n" +
                    "второе по величине УНИКАЛЬНОЕ число в списке.\n\n" +
                    "Если уникальных чисел меньше двух — верни None.\n" +
                    "Повторы не считаются: в [5, 5, 2] второй по величине — 2.",
                Hint = "Можно отсортировать уникальные значения: values = sorted(set(nums)). Не забудь про len(values) < 2.",
                Examples = new[] { "second_largest([1, 3, 2])  ->  2", "second_largest([7])  ->  None" },
                StarterCode =
                    "def second_largest(nums):\n" +
                    "    # Убери повторы и найди второе по величине значение\n" +
                    "    return None\n",
                Tests =
                {
                    new PythonTestCase("second_largest([1, 3, 2])", 2L, NewList(1L, 3L, 2L)),
                    new PythonTestCase("second_largest([5, 5, 2])", 2L, NewList(5L, 5L, 2L)),
                    new PythonTestCase("second_largest([7])", null, NewList(7L)),
                    new PythonTestCase("second_largest([10, 9, 9, 8])", 9L, NewList(10L, 9L, 9L, 8L)),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 10,
                Id = "task_10_prime",
                Title = "Простое число",
                Difficulty = "Сложнее",
                FunctionName = "is_prime",
                Description =
                    "Криптомодуль проверяет ключи. Напиши is_prime(n),\n" +
                    "которая возвращает True, если n — простое число, иначе False.\n\n" +
                    "Простые числа больше 1 делятся только на 1 и на себя.\n" +
                    "Числа меньше 2 не являются простыми.",
                Hint = "Перебирай делители i от 2 до n - 1. Как только n % i == 0 — сразу return False.",
                Examples = new[] { "is_prime(17)  ->  True", "is_prime(9)  ->  False" },
                StarterCode =
                    "def is_prime(n):\n" +
                    "    if n < 2:\n" +
                    "        return False\n" +
                    "    # Проверь делители\n" +
                    "    return True\n",
                Tests =
                {
                    new PythonTestCase("is_prime(2)", true, 2L),
                    new PythonTestCase("is_prime(17)", true, 17L),
                    new PythonTestCase("is_prime(9)", false, 9L),
                    new PythonTestCase("is_prime(1)", false, 1L),
                    new PythonTestCase("is_prime(97)", true, 97L),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 11,
                Id = "task_11_compress",
                Title = "Сжатие строки",
                Difficulty = "Сложнее",
                FunctionName = "compress",
                Description =
                    "Бэкенд экономит трафик. Напиши compress(text), которая сжимает\n" +
                    "подряд идущие одинаковые символы в символ + количество.\n\n" +
                    "\"aaabbc\" -> \"a3b2c1\"\n" +
                    "Пустая строка -> \"\". Каждый символ в результате сопровождается числом.",
                Hint = "Иди по строке и считай одинаковые символы. Для числа используй str(count).",
                Examples = new[] { "compress(\"aaabbc\")  ->  \"a3b2c1\"", "compress(\"abc\")  ->  \"a1b1c1\"" },
                StarterCode =
                    "def compress(text):\n" +
                    "    result = \"\"\n" +
                    "    # Сожми одинаковые символы\n" +
                    "    return result\n",
                Tests =
                {
                    new PythonTestCase("compress(\"aaabbc\")", "a3b2c1", "aaabbc"),
                    new PythonTestCase("compress(\"abc\")", "a1b1c1", "abc"),
                    new PythonTestCase("compress(\"\")", "", ""),
                    new PythonTestCase("compress(\"zzzzzzzzzz\")", "z10", "zzzzzzzzzz"),
                }
            });

            tasks.Add(new PythonTask
            {
                Month = 12,
                Id = "task_12_matrix_sum",
                Title = "Финальный заказ: сумма матрицы",
                Difficulty = "Финал",
                FunctionName = "matrix_sum",
                Description =
                    "Последний заказ перед закрытием ипотеки. Напиши matrix_sum(matrix),\n" +
                    "которая возвращает сумму всех чисел в двумерном списке.\n\n" +
                    "Матрица — список строк, каждая строка — список чисел.\n" +
                    "Пустая матрица даёт 0. Используй вложенные циклы for.",
                Hint = "Сначала for row in matrix, затем for value in row: total += value.",
                Examples = new[] { "matrix_sum([[1, 2], [3, 4]])  ->  10", "matrix_sum([])  ->  0" },
                StarterCode =
                    "def matrix_sum(matrix):\n" +
                    "    total = 0\n" +
                    "    # Пройди по строкам и числам\n" +
                    "    return total\n",
                Tests =
                {
                    new PythonTestCase("matrix_sum([[1, 2], [3, 4]])", 10L, NewList(NewList(1L, 2L), NewList(3L, 4L))),
                    new PythonTestCase("matrix_sum([])", 0L, NewList()),
                    new PythonTestCase("matrix_sum([[5]])", 5L, NewList(NewList(5L))),
                    new PythonTestCase("matrix_sum([[-1, 2], [10, -5]])", 6L, NewList(NewList(-1L, 2L), NewList(10L, -5L))),
                }
            });

            return tasks;
        }

        private static List<object> NewList(params object[] values)
        {
            return new List<object>(values);
        }

        private static Dictionary<string, object> NewDict(params (string key, object value)[] values)
        {
            var dict = new Dictionary<string, object>();
            foreach (var pair in values) dict[pair.key] = pair.value;
            return dict;
        }
    }
}
