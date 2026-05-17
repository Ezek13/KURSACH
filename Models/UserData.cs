using System;
using System.Collections.Generic;

namespace tgbot.Models
{
    public class UserData
    {
        // Базові дані користувача
        public long ChatId { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public double Height { get; set; }
        public double Weight { get; set; }
        public string CurrentState { get; set; } = "None";
        public bool IsMusicEnabled { get; set; } = true;
        public double DailyCaloriesBurned { get; set; } = 0;
        public string Goal { get; set; } = "Підтримка"; 
        

        // Гейміфікація та прогрес
        public int Experience { get; set; } = 0; 
        public int Level => (Experience / 100) + 1; // Рівень 1, 2, 3...

        // СИСТЕМА РАНГІВ
        public string Rank => Level switch
        {
            < 3 => "Новачок 🌱",
            < 7 => "Ентузіаст ⚡",
            < 15 => "Атлет KPI 🏅",
            < 25 => "Майстер спорту 🏆",
            < 50 => "Сталевий Гігант 🦾",
            _ => "Легенда КПІ 🔥"
        };

        public int StreakCount { get; set; } = 0;   // Серія днів тренувань
        public DateTime LastTrainingDate { get; set; } = DateTime.MinValue;

        // Історія тренувань
        public List<ExerciseRecord> CompletedExercises { get; set; } = new();
    }

    public class ExerciseRecord
    {
        public string ExerciseName { get; set; } = "";
        public DateTime Date { get; set; }
    }
}