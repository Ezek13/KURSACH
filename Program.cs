using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using tgbot.Models;

namespace tgbot
{
    class Program
    {
        private static ITelegramBotClient botClient = new TelegramBotClient("8570826679:AAGs4geWPyji9x217DQ69K-eco48XKQcOpA");
        private const string DbPath = "users.json";
        private static int _activeTrainingsCount = 0; 

        private static readonly Dictionary<string, (int WorkSec, int RestSec, string SearchTerm)> ExerciseData = new()
        {
            // Силові 
            { "Віджимання", (45, 30, "pushups") },
            { "Присідання", (45, 30, "squats") },
            { "Випади", (45, 30, "lunges") },
            { "Планка", (60, 30, "plank") },
            { "Підйом тазу", (45, 30, "glute bridge") },

            // Кардіо 
            { "Біг на місці", (60, 20, "running") },
            { "Стрибки", (45, 15, "jumping jacks") },
            { "Швидка ходьба", (120, 30, "walking") },
            { "Танці", (180, 60, "dancing") },

            // HIIT 
            { "Бурпі", (30, 15, "burpees") },
            { "Спринти на місці", (20, 10, "high knees") },
            { "Альпініст", (40, 20, "mountain climbers") },
            { "Присідання зі стрибком", (30, 15, "jump squats") },

            // Функціональні 
            { "Планка з рухами", (45, 20, "dynamic plank") },
            { "Випади з поворотом", (45, 20, "lunge with twist") },
            { "Баланс на одній нозі", (40, 15, "single leg balance") },
            { "Повільні присідання", (60, 30, "slow squats") },

            // Розтяжка 
            { "Розтяжка ніг", (40, 10, "leg stretch") },
            { "Розтяжка спини", (40, 10, "back stretch") },
            { "Нахили вперед", (30, 10, "toe touch") },
            { "Рухливість суглобів", (60, 0, "joint mobility") },
            { "Легка йога", (300, 60, "yoga") }
        };

        static async Task Main()
        {
            using var cts = new CancellationTokenSource();
            botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, new ReceiverOptions(), cts.Token);
            LoggerService.Log("Бот запущений");
            UpdateConsoleStatus();
            await Task.Delay(-1);
        }

static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
{
    try 
    {
        var users = LoadData();

        // 1. ОБРОБКА КНОПОК (CallbackQuery - Цілі та Вода)
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
        {
            var data = update.CallbackQuery.Data;
            long cbChatId = update.CallbackQuery.Message.Chat.Id;
            var cbUser = users.Find(u => u.ChatId == cbChatId);

            await bot.AnswerCallbackQuery(update.CallbackQuery.Id);

            if (data.StartsWith("setgoal_"))
            {
                string goalText = data switch
                {
                    "setgoal_loss" => "Схуднення",
                    "setgoal_mass" => "Набір маси",
                    _ => "Підтримка"
                };

                // ВИКЛИК PUT: передаємо нову ціль ТА поточну вагу 
                bool isApiUpdated = await SettingsService.UpdateUserGoal(cbChatId, goalText, cbUser?.Weight ?? 0);

                if (isApiUpdated && cbUser != null)
                {
                    cbUser.Goal = goalText;
                    SaveData(users);
                    await bot.EditMessageText(cbChatId, update.CallbackQuery.Message.MessageId, $"✅ Ціль «{goalText}» успішно оновлена!", cancellationToken: ct);
                }
                return;
            }

            if (data == "water_drunk" && cbUser != null)
            {
                cbUser.Experience += 5;
                SaveData(users);
                await bot.EditMessageText(cbChatId, update.CallbackQuery.Message.MessageId, "✅ Водний баланс поповнено! (+5 XP)", cancellationToken: ct);
                return;
            }
            return;
        }

        // 2. ПЕРЕВІРКА НА ТЕКСТОВЕ ПОВІДОМЛЕННЯ
        if (update.Message is not { Text: { } messageText } message) return;
        long chatId = message.Chat.Id;
        var user = users.Find(u => u.ChatId == chatId) ?? new UserData { ChatId = chatId, IsMusicEnabled = true };

        // ОБРОБКА СТАНУ: Введення нової ваги
        if (user.CurrentState == "UpdatingWeight")
        {
            if (double.TryParse(messageText.Replace(',', '.'), out double newWeight))
            {
                // ВИКЛИК PUT: передаємо нову вагу ТА поточну ціль 
                bool isApiUpdated = await SettingsService.UpdateUserWeight(chatId, newWeight, user.Goal);

                if (isApiUpdated)
                {
                    user.Weight = newWeight;
                    user.CurrentState = "None"; 
                    SaveData(users);
                    await bot.SendMessage(chatId, $"✅ Вагу оновлено до {newWeight} кг та синхронізовано!", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(chatId, "❌ Помилка API при оновленні ваги.", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                }
            }
            else
            {
                await bot.SendMessage(chatId, "❌ Будь ласка, введіть число.");
            }
            return;
        }
        
        if (user.CurrentState == "SearchingExercise")
        {
            await bot.SendMessage(chatId, "🔍 Шукаю інформацію та перекладаю... Зачекайте.");

            string exerciseDetails = await NinjaService.GetExerciseDetailsAsync(messageText);

            user.CurrentState = "None"; 
            SaveData(users);

            await bot.SendMessage(chatId, exerciseDetails, 
                parseMode: ParseMode.Markdown, 
                replyMarkup: GetTrainingMenu(), 
                cancellationToken: ct);
            return; 
        }

        // Логіка реєстрації
        if (user.CurrentState != "None" && user.CurrentState != "Completed")
        {
            await ProcessRegistration(bot, user, messageText, ct);
            SaveData(users);
            return;
        }

      

// 2. Обробка спеціальних команд (Музика тощо)
        if (messageText.Contains("Музика"))
        {
            user.IsMusicEnabled = !user.IsMusicEnabled;
            string status = user.IsMusicEnabled ? "увімкнено ✅" : "вимкнено ❌";
            SaveData(users);
            await bot.SendMessage(chatId, $"🎧 Режим музики {status}!", replyMarkup: GetMainMenu(user), cancellationToken: ct);
            return; 
        }
        
        else
        {
            switch (messageText)
            {
                case "Пошук вправи 🔍":
                    user.CurrentState = "SearchingExercise";
                    SaveData(users); 
                    await bot.SendMessage(chatId, "Напишіть назву вправи українською (наприклад: жим лежачи):", 
                        replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    break;
                case "Тренування 🏋️":
                    await bot.SendMessage(chatId, "Оберіть категорію:", replyMarkup: GetTrainingMenu(), cancellationToken: ct);
                    break;
                
                case "/start":
                case "Головне меню 🏠":
                    await bot.SendMessage(chatId, "Оберіть потрібний розділ:", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    break;

                case "Ввести дані 📝":
                    user.CurrentState = "WaitingName";
                    await bot.SendMessage(chatId, "Як вас звати?", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    break;
                
                case "Оновити вагу ⚖️":
                    user.CurrentState = "UpdatingWeight";
                    SaveData(users);
                    await bot.SendMessage(chatId, "Введіть вашу нову вагу:", replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "Налаштувати ціль 🎯":
                    var goalButtons = new InlineKeyboardMarkup(new[]
                    {
                        // Перший параметр - текст на кнопці, другий - callback_data 
                        new [] { InlineKeyboardButton.WithCallbackData("Схуднення 📉", "setgoal_loss") },
                        new [] { InlineKeyboardButton.WithCallbackData("Набір маси 💪", "setgoal_mass") },
                        new [] { InlineKeyboardButton.WithCallbackData("Підтримка ⚖️", "setgoal_stay") }
                    });

                    await bot.SendMessage(chatId, "Оберіть вашу нову ціль.", 
                        replyMarkup: goalButtons, cancellationToken: ct);
                    break;
                
                case "Мій профіль 👤":
                    if (string.IsNullOrEmpty(user.Name)) {
                        await bot.SendMessage(chatId, "❌ Профіль порожній. Заповніть дані!", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    } else {
                        double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
                        
                        string profile = $"📋 *ВАШ ПРОФІЛЬ*\n" +
                                         $"━━━━━━━━━━━━━━━\n" +
                                         $"👤 *Ім'я:* {user.Name}\n" +
                                         $"🎯 *Поточна ціль:* {user.Goal ?? "Не встановлено"}\n" +
                                         $"🎖 *Ранг:* {user.Rank}\n" + // Твій новий ранг!
                                         $"🏆 *Рівень:* {user.Level} (До наст.: {100 - (user.Experience % 100)} XP)\n" +
                                         $"🔥 *Серія:* {user.StreakCount} днів\n" +
                                         $"━━━━━━━━━━━━━━━\n" +
                                         $"📏 *Зріст:* {user.Height} см | ⚖️ *Вага:* {user.Weight} кг\n" +
                                         $"📊 *ВМІ:* {Math.Round(bmi, 1)} ({HealthCalculator.GetBMICategory(bmi)})\n" +
                                         $"🎵 *Музика:* {(user.IsMusicEnabled ? "✅" : "❌")}";

                        await bot.SendMessage(chatId, profile, parseMode: ParseMode.Markdown, replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    }
                    break;

                case "Таблиця лідерів 🏆":
                    var topUsers = users
                        .OrderByDescending(u => u.Experience)
                        .Take(5) 
                        .ToList();

                    string leaderboard = "🏆 *ТОП-5 АТЛЕТІВ KPI:*\n\n";
                    for (int i = 0; i < topUsers.Count; i++)
                    {
                        string medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "👤" };
                        leaderboard += $"{medal} {topUsers[i].Name ?? "Анонім"} — {topUsers[i].Experience} XP\n";
                    }

                    if (!topUsers.Any()) leaderboard = "📭 Таблиця лідерів поки порожня.";

                    await bot.SendMessage(chatId, leaderboard, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;

                case "Здоров'я 🍎":
                    if (user.Weight == 0 || user.Height == 0) 
                    {
                        await bot.SendMessage(chatId, "⚠️ Спочатку заповніть профіль (вага та зріст), щоб я міг розрахувати показники!", 
                            replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    } 
                    else 
                    {
                        double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
                        string bmiCategory = HealthCalculator.GetBMICategory(bmi);
                        double bmr = HealthCalculator.CalculateBMR(user);
        
                        double maintenanceCalories = bmr * 1.2; 
        
                        string personalAdvice = HealthCalculator.GetPersonalAdvice(bmi);

                        string healthInfo = $"🍎 *ВАШІ ПОКАЗНИКИ ЗДОРОВ'Я*\n" +
                                            $"━━━━━━━━━━━━━━━━━━\n" +
                                            $"📊 *ІМТ:* {Math.Round(bmi, 1)} — _{bmiCategory}_\n" +
                                            $"🔥 *Базовий метаболізм:* {Math.Round(bmr)} ккал\n" +
                                            $"🏃 *Норма для підтримки ваги:* {Math.Round(maintenanceCalories)} ккал\n" +
                                            $"💧 *Денна норма води:* {Math.Round(user.Weight * 0.03, 1)} л\n" +
                                            $"━━━━━━━━━━━━━━━━━━\n" +
                                            $"{personalAdvice}\n\n" +
                                            $"💡 *Випадкова порада:* _{HealthService.GetRandomTip()}_";

                        await bot.SendMessage(chatId, healthInfo, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    }
                    break;
                
                
                
                case "Статистика 📊":
                    string statsReport = StatisticsService.GetUserReport(user);
                    await bot.SendMessage(chatId, statsReport, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;
                
                
                case "Назад до тренувань 🔙":
                    await bot.SendMessage(chatId, "Оберіть категорію:", replyMarkup: GetTrainingMenu(), cancellationToken: ct);
                    break;

                case "Силові 💪": await bot.SendMessage(chatId, "💪 Оберіть силову вправу:", replyMarkup: GetStrengthMenu(), cancellationToken: ct); break;
                case "Кардіо ❤️": await bot.SendMessage(chatId, "❤️ Оберіть кардіо:", replyMarkup: GetCardioMenu(), cancellationToken: ct); break;
                case "HIIT ⚡": await bot.SendMessage(chatId, "⚡ Оберіть HIIT вправу:", replyMarkup: GetHIITMenu(), cancellationToken: ct); break;
                case "Функціональні 🧘": await bot.SendMessage(chatId, "🧘 Оберіть вправу:", replyMarkup: GetFunctionalMenu(), cancellationToken: ct); break;
                case "Розтяжка 🧩": await bot.SendMessage(chatId, "🧩 Оберіть вправу:", replyMarkup: GetStretchMenu(), cancellationToken: ct); break;
                case "Авто-підбір 🤖": await SuggestTraining(bot, user, ct); break;
                
                default:
                    if (ExerciseData.ContainsKey(messageText)) 
                        await SendExerciseVideo(bot, user, messageText, ct);
                    break;
            }
        }
        
        if (!users.Exists(u => u.ChatId == chatId)) users.Add(user);
        SaveData(users);
    }
    catch (Exception ex)
    {
        LoggerService.Log($"Critical error in HandleUpdateAsync: {ex.Message}");
    }
}

        private static async Task SendExerciseVideo(ITelegramBotClient bot, UserData user, string exName, CancellationToken ct)
        {
            if (ExerciseData.TryGetValue(exName, out var data)) 
            {
                try 
                {
                    string ninjaDetails = await NinjaService.GetExerciseDetailsAsync(data.SearchTerm);
                    
            
                    string videoUrl = await PexelsService.GetExerciseVideoAsync(data.SearchTerm);

                    string caption = $"🏋️ *Вправа:* {exName.ToUpper()}\n" +
                                     $"⏱ *Час:* {data.WorkSec}с робота / {data.RestSec}с відпочинок\n" +
                                     $"━━━━━━━━━━━━━━━━\n" +
                                     $"{ninjaDetails}";

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        await bot.SendAnimation(
                            chatId: user.ChatId,
                            animation: InputFile.FromUri(videoUrl),
                            caption: caption,
                            parseMode: ParseMode.Markdown,
                            cancellationToken: ct);
                    }
                    else 
                    {
                        await bot.SendMessage(user.ChatId, caption, ParseMode.Markdown, cancellationToken: ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    await bot.SendMessage(user.ChatId, $"⚠️ Починаємо вправу: {exName}");
                }

                _ = Task.Run(() => RunExerciseLifecycle(bot, user.ChatId, data.WorkSec, data.RestSec, exName, ct));
            }
        }

        private static async Task RunExerciseLifecycle(ITelegramBotClient bot, long chatId, int work, int rest, string exName, CancellationToken ct)
{
    Interlocked.Increment(ref _activeTrainingsCount);
    UpdateConsoleStatus();

    try 
    {
        await StartLiveTimer(bot, chatId, work, $"🔥 Виконуємо: {exName}", ct);

        var allUsers = LoadData();
        var u = allUsers.Find(usr => usr.ChatId == chatId);
        if (u != null)
        {
            u.Experience += work;
            double burned = HealthCalculator.CalculateBurnedCalories(exName, u.Weight, work);
            

            UpdateStreak(u);
            u.CompletedExercises.Add(new ExerciseRecord { ExerciseName = exName, Date = DateTime.Now });
            SaveData(allUsers);

            await bot.SendMessage(chatId, 
                $"✅ *Вправу завершено!*\n" +
                $"🌟 +{work} XP | 🔥 ~{Math.Round(burned, 1)} ккал", 
                parseMode: ParseMode.Markdown, cancellationToken: ct);
        }

        if (rest > 0) 
        {
            await StartLiveTimer(bot, chatId, rest, "🛌 Відпочинок:", ct);
            await bot.SendMessage(chatId, "🔔 Відпочинок закінчено! Готові далі?", cancellationToken: ct);
        }

        _ = Task.Run(async () => {
            await Task.Delay(60000); 
            try {
                var waterKeyboard = new InlineKeyboardMarkup(new[] {
                    InlineKeyboardButton.WithCallbackData("Я випив води! 💧", "water_drunk")
                });
                await bot.SendMessage(chatId, "🔔 *Поповніть водний баланс!*", parseMode: ParseMode.Markdown, replyMarkup: waterKeyboard);
            } catch { }
        });
    }
    finally 
    {
        Interlocked.Decrement(ref _activeTrainingsCount);
        UpdateConsoleStatus();
    }
}

        private static void UpdateStreak(UserData user)
        {
            DateTime today = DateTime.Today;
            if (user.LastTrainingDate.Date == today) return;

            if ((today - user.LastTrainingDate.Date).Days == 1)
                user.StreakCount++;
            else
                user.StreakCount = 1;

            user.LastTrainingDate = today;
        }

        private static void UpdateConsoleStatus() => 
            Console.Title = $"Active Workouts: {_activeTrainingsCount} | KPI Fitness Bot";

        private static async Task StartLiveTimer(ITelegramBotClient bot, long chatId, int seconds, string title, CancellationToken ct)
        {
            try 
            {
                var statusMessage = await bot.SendMessage(chatId, $"{title} {seconds}с", cancellationToken: ct);

                for (int i = seconds - 1; i >= 0; i--)
                {
                    await Task.Delay(1000, ct); 
                    if (i % 5 == 0 || i <= 5)
                    {
                        try {
                            await bot.EditMessageText(chatId, statusMessage.MessageId, i > 0 ? $"{title} {i}с" : $"✅ {title} завершено!", cancellationToken: ct);
                        } catch { }
                    }
                }
            } catch { }
        }

        private static ReplyKeyboardMarkup GetMainMenu(UserData user)
        {
            // Динамічна кнопка музики
            string musicBtn = user.IsMusicEnabled ? "Музика ✅" : "Музика ❌";

            return new ReplyKeyboardMarkup(new[] 
            { 
                // Перший ряд: Профіль та введення даних
                new KeyboardButton[] { "Ввести дані 📝", "Мій профіль 👤" }, 

                // Другий ряд: Тренування та твоє Здоров'я
                new KeyboardButton[] { "Тренування 🏋️", "Здоров'я 🍎" },

                // Третій ряд: Статистика та Лідерборд
                new KeyboardButton[] { "Статистика 📊", "Таблиця лідерів 🏆" },

                // Четвертий ряд: Дві окремі кнопки (виправлено синтаксис)
                new KeyboardButton[] { "Налаштувати ціль 🎯", "Оновити вагу ⚖️" },

                // П'ятий ряд: Керування музикою та кнопка повернення
                new KeyboardButton[] { musicBtn, "Головне меню 🏠" } 
            }) 
            { 
                ResizeKeyboard = true 
            };
        }
        
        
        private static async Task SuggestTraining(ITelegramBotClient bot, UserData user, CancellationToken ct)
        {
            if (user.Height == 0 || user.Weight == 0) {
                await bot.SendMessage(user.ChatId, "⚠️ Заповніть профіль для авто-підбору!");
                return;
            }
        
            double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
            List<string> selectedExercises = bmi switch {
                < 18.5 => new List<string> { "Віджимання", "Присідання", "Прес" },
                >= 25 => new List<string> { "Бурпі", "Стрибки", "Альпініст" },
                _ => new List<string> { "Планка", "Випади", "Стрибки" }
            };
        
            await bot.SendMessage(user.ChatId, $"🤖 *Авто-підбір активовано!*\nВаш ІМТ: {Math.Round(bmi, 1)}. Підготовлено тренування з {selectedExercises.Count} вправ.", parseMode: ParseMode.Markdown);
        
            _ = Task.Run(async () => 
            {
                foreach (var ex in selectedExercises)
                {
                    if (ExerciseData.TryGetValue(ex, out var data))
                    {
                        await bot.SendMessage(user.ChatId, $"🚀 Наступна вправа: *{ex}*", parseMode: ParseMode.Markdown);
                
                        await RunExerciseLifecycle(bot, user.ChatId, data.WorkSec, data.RestSec, ex, ct);
                    }
                }
                await bot.SendMessage(user.ChatId, "🏆 Комплекс завершено!");
            });
        
            await bot.SendMessage(user.ChatId, "🏆 *Тренування завершено!* Ви молодець!");
        }

        private static async Task ProcessRegistration(ITelegramBotClient bot, UserData user, string text, CancellationToken ct)
        {
            try 
            {
                switch (user.CurrentState)
                {
                    case "WaitingName": user.Name = text; user.CurrentState = "WaitingAge"; await bot.SendMessage(user.ChatId, "Ваш вік?", cancellationToken: ct); break;
                    case "WaitingAge": if (int.TryParse(text, out int a)) { user.Age = a; user.CurrentState = "WaitingHeight"; await bot.SendMessage(user.ChatId, "Ваш зріст (см)?", cancellationToken: ct); } break;
                    case "WaitingHeight": if (double.TryParse(text, out double h)) { user.Height = h; user.CurrentState = "WaitingWeight"; await bot.SendMessage(user.ChatId, "Ваша вага (кг)?", cancellationToken: ct); } break;
                    case "WaitingWeight": if (double.TryParse(text, out double w)) { user.Weight = w; user.CurrentState = "Completed"; await bot.SendMessage(user.ChatId, "Анкету заповнено!", replyMarkup: GetMainMenu(user), cancellationToken: ct); } break;
                }
            } catch { }
        }

        private static ReplyKeyboardMarkup GetTrainingMenu() => new(new[] 
        { 
            new KeyboardButton[] { "Пошук вправи 🔍" }, // Нова кнопка для твого NinjaService
            new KeyboardButton[] { "Авто-підбір 🤖" },
            new KeyboardButton[] { "Силові 💪", "Кардіо ❤️" }, 
            new KeyboardButton[] { "HIIT ⚡", "Функціональні 🧘" }, 
            new KeyboardButton[] { "Розтяжка 🧩" },
            new KeyboardButton[] { "Головне меню 🏠" } 
        }) { ResizeKeyboard = true };

        private static ReplyKeyboardMarkup GetStrengthMenu() => new(new[] { new KeyboardButton[] { "Віджимання", "Присідання" }, new KeyboardButton[] { "Випади", "Планка" }, new KeyboardButton[] { "Підйом тазу" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetCardioMenu() => new(new[] { new KeyboardButton[] { "Біг на місці", "Стрибки" }, new KeyboardButton[] { "Швидка ходьба", "Танці" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetHIITMenu() => new(new[] { new KeyboardButton[] { "Бурпі", "Спринти на місці" }, new KeyboardButton[] { "Альпініст", "Присідання зі стрибком" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetFunctionalMenu() => new(new[] { new KeyboardButton[] { "Планка з рухами", "Випади з поворотом" }, new KeyboardButton[] { "Баланс на одній нозі", "Повільні присідання" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetStretchMenu() => new(new[] { new KeyboardButton[] { "Розтяжка ніг", "Розтяжка спини" }, new KeyboardButton[] { "Нахили вперед", "Рухливість суглобів" }, new KeyboardButton[] { "Легка йога" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };

        private static List<UserData> LoadData() 
        {
            try { return File.Exists(DbPath) ? JsonSerializer.Deserialize<List<UserData>>(File.ReadAllText(DbPath)) ?? new() : new(); }
            catch { return new List<UserData>(); }
        }

        private static void SaveData(List<UserData> data) 
        {
            try { File.WriteAllText(DbPath, JsonSerializer.Serialize(data)); }
            catch (Exception ex) { LoggerService.Log($"Save error: {ex.Message}"); }
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient b, Exception e, CancellationToken c) 
        {
            LoggerService.Log($"Polling Error: {e.Message}");
            return Task.CompletedTask; 
        }
    }

    public static class LoggerService
    {
        private const string LogFile = "bot_logs.txt";
        public static void Log(string message)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(entry);
            try { File.AppendAllText(LogFile, entry + Environment.NewLine); } catch { }
        }
    }
}