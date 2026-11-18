using System;

namespace MortgageSim.Core
{
    /// <summary>Экономика игры: 12 месяцев, 12 платежей и 12 зарплат.</summary>
    public static class GameBalance
    {
        public const int Months = 12;
        public const int StartMoney = 100;
        public const float PlayerSpeed = 3.4f;
        public const float InteractionRange = 3.1f;

        /// <summary>Платёж коллектору в каждом месяце.</summary>
        public static readonly int[] Payments =
        {
            600, 620, 640, 660, 680, 700,
            720, 740, 760, 780, 800, 900
        };

        /// <summary>Зарплата за успешно сданную задачу.</summary>
        public static readonly int[] Salaries =
        {
            650, 670, 690, 710, 730, 750,
            770, 790, 810, 830, 850, 950
        };

        public static int TotalDebt
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Payments.Length; i++) total += Payments[i];
                return total;
            }
        }

        public static int PaymentForMonth(int month)
        {
            int index = Math.Max(0, Math.Min(Payments.Length - 1, month - 1));
            return Payments[index];
        }

        public static int SalaryForMonth(int month)
        {
            int index = Math.Max(0, Math.Min(Salaries.Length - 1, month - 1));
            return Salaries[index];
        }

        public static string Money(int value)
        {
            return "$" + value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
        }
    }
}
