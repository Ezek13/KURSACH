using System;

namespace tgbot.Models
{
    public static class HealthCalculator
    {
        // 1. Розрахунок ІМТ (BMI)
        public static double CalculateBMI(double weight, double height) 
            => weight / Math.Pow(height / 100, 2);

        // 2. Інтерпретація ІМТ
        public static string GetBMICategory(double bmi) => bmi switch
        {
            < 18.5 => "Недостатня вага 🦴",
            < 25 => "Норма ✅",
            < 30 => "Надмірна вага ⚠️",
            _ => "Ожиріння 🚨"
        };
        
        public static double CalculateBMR(UserData user)
        {
            return (10 * user.Weight) + (6.25 * user.Height) - (5 * user.Age) + 5;
        }

        public static double CalculateBurnedCalories(string exerciseName, double weight, int durationSec)
        {
            double met = exerciseName switch
            {
                "Бурпі" => 10.0, 
                "Спринти на місці" => 12.0,
                "Віджимання" => 8.0,
                "Планка" => 3.0,
                "Біг на місці" => 9.0, 
                "Розтяжка ніг" => 2.5,
                "Альпініст" => 10.0,
                "Стрибки" => 8.0,
                _ => 5.0 
            };

            double durationMin = durationSec / 60.0;
            return (met * 3.5 * weight / 200) * durationMin;
        }

        public static string GetPersonalAdvice(double bmi)
        {
            return bmi switch
            {
                < 18.5 => "💡 Порада: Зосередьтеся на силових вправах 💪 та збільште споживання білків.",
                < 25 => "💡 Порада: Ви у чудовій формі! Підтримуйте активність різноманітними вправами 🧘.",
                _ => "💡 Порада: Рекомендуємо додати більше кардіо ❤️ та HIIT ⚡ для ефективного спалювання калорій."
            };
        }
    }
}