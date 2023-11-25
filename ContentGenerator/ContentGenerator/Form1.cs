using System;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Krypton.Toolkit;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Data.SQLite;
using System.IO;
using System.Speech.Synthesis;

namespace ContentGenerator
{
    public partial class Form1 : KryptonForm
    {
        private const string connectionString = "Data Source=History.db;Version=3;";
        private TextManager textManager;
        private TextToSpeechManager textToSpeechManager;
        public Form1()
        {
            InitializeComponent();
            kryptonButton1.Enabled = false;
            kryptonButton2.Enabled = false;
            kryptonButton3.Enabled = false;

            textManager = new TextManager(connectionString, kryptonRichTextBox1);
            textManager.Initialize();
            textToSpeechManager = new TextToSpeechManager();

            kryptonRichTextBox1.ReadOnly = false;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            textManager.SaveTextsBeforeExit(e);
        }

        ///
        ///Кнопки для работы с текстом.
        /// 
        private async void kryptonButton1_Click(object sender, EventArgs e)//Генерация текста
        {
            kryptonButton1.Enabled = false;
            await textManager.GenerateText(kryptonTextBox1.Text, kryptonTextBox2.Text);
            kryptonButton1.Enabled = true;
        }
        private async void kryptonButton2_Click(object sender, EventArgs e)//Корректировка
        {
            await textManager.CorrectText();
        }
        private async void kryptonButton3_Click(object sender, EventArgs e)//Добавление подробностей
        {
            await textManager.AddDetails(kryptonTextBox2.Text);
        }

        //Переключение сохраненных текстов в истории и логика работы отображения кнопок.
        private void kryptonButton4_Click(object sender, EventArgs e)
        {
            textManager.ShowPreviousText();
        }
        private void kryptonButton6_Click(object sender, EventArgs e)
        {
            textManager.ShowNextText();
        }
        private void kryptonRichTextBox1_TextChanged(object sender, EventArgs e)
        {
            if (kryptonRichTextBox1.Text.Length < 25)
            {
                kryptonButton2.Enabled = false;
                kryptonButton3.Enabled = false;
            }
            else 
            {
                kryptonButton2.Enabled = true;
                kryptonButton3.Enabled = true;
            }
        }
        private void kryptonTextBox1_TextChanged(object sender, EventArgs e)
        {
            if (kryptonTextBox1.Text.Length < 4)
            {
                kryptonButton1.Enabled = false;
            }
            else
            {
                kryptonButton1.Enabled = true;
            }
            kryptonTextBox3.Text = kryptonTextBox1.Text;
        }
        ///
        /// Вкладка "Изображение"
        ///
        private void kryptonButton8_Click(object sender, EventArgs e)//Отображение изображения в отдельном окне
        {
            if (pictureBox1.Image != null)
            {
                KryptonForm imageForm = new KryptonForm();
                PictureBox imagePictureBox = new PictureBox();
                imagePictureBox.Image = pictureBox1.Image;
                imagePictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                imagePictureBox.Dock = DockStyle.Fill;
                imageForm.Controls.Add(imagePictureBox);
                imageForm.Size = new Size(1280, 720);
                imageForm.ShowDialog();
            }
        }
        private void kryptonButton7_Click(object sender, EventArgs e)//Сохранение изображения
        {
            if (pictureBox1.Image != null)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Изображения|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                saveFileDialog.Title = "Сохранить изображение";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    try
                    {
                        pictureBox1.Image.Save(filePath);
                        MessageBox.Show("Изображение сохранено.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при сохранении изображения: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Нет изображения для сохранения.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private async void kryptonButton5_Click_1(object sender, EventArgs e)//Нажатие кнопки генерации изображения
        {
            ImageManager imageManager = new ImageManager(pictureBox1);
            await imageManager.GenerateImage(kryptonTextBox3.Text, kryptonRichTextBox2.Text);
        }
        ///
        /// Вкладка "Озвучивание"
        ///
        private void kryptonButton9_Click(object sender, EventArgs e)//Прослушивание текста в исполнении голосового синтеза речи
        {
            string textToSpeak = kryptonRichTextBox3.Text;
            textToSpeechManager.SpeakText(textToSpeak);
        }

        private void kryptonButton12_Click(object sender, EventArgs e)//Сохранение озвученного текста
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Аудиофайл|*.wav";
            saveFileDialog.Title = "Сохранить аудиофайл";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = saveFileDialog.FileName;
                string textToSave = kryptonRichTextBox3.Text;

                if (!string.IsNullOrEmpty(textToSave))
                {
                    using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
                    {
                        synthesizer.SetOutputToWaveFile(filePath);
                        synthesizer.Speak(textToSave);
                        synthesizer.SetOutputToDefaultAudioDevice();
                    }
                }
            }
        }

        private void kryptonButton11_Click(object sender, EventArgs e)//Перенос текста из первой вкладки
        {
            kryptonRichTextBox3.Text = kryptonRichTextBox1.Text;
        }
    }
    public class TextManager
    {
        private readonly string connectionString;
        private readonly KryptonRichTextBox richTextBox;
        private List<string> savedTexts = new List<string>();
        private int currentIndex = -1;
        private int maxRecordCount = 15;
        public readonly OpenAIService openAIService;

        public TextManager(string connectionString, KryptonRichTextBox richTextBox)
        {
            this.connectionString = connectionString;
            this.richTextBox = richTextBox;
            var apiKey = "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";//Буду очень признателен если его как можно меньшее количество людей увидит.)
            openAIService = new OpenAIService(new OpenAiOptions
            {
                ApiKey = apiKey
            });
            openAIService.SetDefaultModelId(Models.Gpt_4);
        }

        public void Initialize()
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))//Обычный коннект к бд и запрос по выделению всей таблицы с текстом.
            {
                connection.Open();
                string countQuery = "SELECT COUNT(*) FROM Texts";
                using (SQLiteCommand cmd = new SQLiteCommand(countQuery, connection))
                {
                    int textCount = Convert.ToInt32(cmd.ExecuteScalar());
                    if (textCount > 0)
                    {
                        if (MessageBox.Show("У вас есть сохраненные тексты. Хотите использовать их?", "Сообщение", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            LoadTextsFromDatabase();
                        }
                        else
                        {
                            ClearDatabase();
                        }
                    }
                }
            }
        }

        public async Task GenerateText(string topic, string narratorInfo)
        {
            richTextBox.Cursor = Cursors.WaitCursor;
            richTextBox.ReadOnly = true;
            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("Ты должен написать текст по теме с манерой речи рассказчика, не переводи названия и имена и будь точен."),
                ChatMessage.FromUser($"Тема: {topic}\nРассказчик: {narratorInfo}")
            };
            var req = new ChatCompletionCreateRequest
            {
                Messages = messages,
                N = 1,
                MaxTokens = 900 //Есть ещё параметр "Temperature", но он является ситуативным.
            };
            var res = await openAIService.ChatCompletion.CreateCompletion(req);
            richTextBox.Cursor = Cursors.IBeam;
            if (res.Successful)
            {
                string generatedText = res.Choices.First().Message.Content;
                richTextBox.Text = generatedText;
            }
            else
            {
                richTextBox.Text = "Не удалось получить ответ от GPT-3.";
            }
            savedTexts.Add(richTextBox.Text);
            SaveTextsToDatabase();

            currentIndex = savedTexts.Count - 1;
            richTextBox.ReadOnly = false;
        }

        public async Task CorrectText()
        {
            richTextBox.Cursor = Cursors.WaitCursor;
            richTextBox.ReadOnly = true;
            if (richTextBox.Text.Length <= 150 || richTextBox.Text.Length >= 10000)
            {
                MessageBox.Show("Ошибка: текст отформатирован неверно, пожалуйста, введите от 150 до 10000 символов в поле");
                return;
            }
            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("Ты должен откорректировать текст, исправить пунктуационные и грамматические ошибки, НЕ ДОБАВЛЯТЬ НИКАКОЙ ИНОЙ ИНФОРМАЦИИ, Максимальное количество токенов = 1200."),//На удивление, крик КАПС ЛОКОМ работает даже с нейросетью.
                ChatMessage.FromUser($"\n{richTextBox.Text}")
            };
            var req = new ChatCompletionCreateRequest
            {
                Messages = messages,
                N = 1,
                MaxTokens = 1200              
            };
            var res = await openAIService.ChatCompletion.CreateCompletion(req);
            richTextBox.Cursor = Cursors.IBeam;
            if (res.Successful)
            {
                string generatedText = res.Choices.First().Message.Content;
                richTextBox.Text = generatedText;
            }
            else
            {
                richTextBox.Text = "Не удалось получить ответ от GPT-4.";
            }
            savedTexts.Add(richTextBox.Text);

            currentIndex = savedTexts.Count - 1;
            richTextBox.ReadOnly = false;
            SaveTextsToDatabase();
        }

        public async Task AddDetails(string narratorInfo)
        {
            richTextBox.Cursor = Cursors.WaitCursor;
            richTextBox.ReadOnly = true;

            if (richTextBox.Text.Length <= 100 || richTextBox.Text.Length >= 8000)
            {
                MessageBox.Show("Ошибка: текст отформатирован неверно, пожалуйста, введите от 100 до 8000 символов в поле");
                return;
            }
            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("Ты должен увеличить количество подробностей в тексте, сохранив манеру речи рассказчика. Добавь деталей. Максимальное количество токенов = 1400"),
                ChatMessage.FromUser($"Манера речи рассказчика: {richTextBox.Text}\n Текст:\n{richTextBox.Text}")
            };

            var req = new ChatCompletionCreateRequest
            {
                Messages = messages,
                N = 1,
                MaxTokens = 1400//на 2-4 тысячи больше чем обычно, после перехода на 16к-токеновую модель.
            };

            var res = await openAIService.ChatCompletion.CreateCompletion(req);
            richTextBox.Cursor = Cursors.IBeam;
            if (res.Successful)
            {
                string generatedText = res.Choices.First().Message.Content;
                richTextBox.Text = generatedText;
            }
            else
            {
                richTextBox.Text = "Не удалось получить ответ от GPT-3.";
            }

            savedTexts.Add(richTextBox.Text);
            currentIndex = savedTexts.Count - 1;
            richTextBox.ReadOnly = false;
            SaveTextsToDatabase();
        }

        public void ShowPreviousText()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                richTextBox.Text = savedTexts[currentIndex];
            }
        }

        public void ShowNextText()
        {
            if (currentIndex < savedTexts.Count - 1)
            {
                currentIndex++;
                richTextBox.Text = savedTexts[currentIndex];
            }
        }

        public void SaveTextsBeforeExit(FormClosingEventArgs e)
        {
            if (savedTexts.Count > 0 || richTextBox.Text.Length > 0)
            {
                DialogResult result = MessageBox.Show("У вас есть несохранённые тексты. Хотите сохранить их перед выходом?", "Сохранение текстов", MessageBoxButtons.YesNoCancel);

                if (result == DialogResult.Yes)
                {
                    SaveTextsToDatabase();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (result == DialogResult.No)
                {
                    ClearDatabase();
                }
            }
        }

        public void LoadTextsFromDatabase()
        {
            savedTexts.Clear();
            richTextBox.Clear();
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string selectQuery = "SELECT GeneratedText FROM Texts";
                using (SQLiteCommand cmd = new SQLiteCommand(selectQuery, connection))
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string generatedText = reader["GeneratedText"].ToString();
                        savedTexts.Add(generatedText);
                    }
                }
            }
            if (savedTexts.Count > 0)
            {
                richTextBox.Text = savedTexts[0];
            }
        }

        private void SaveTextsToDatabase()
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string countQuery = "SELECT COUNT(*) FROM Texts";
                using (SQLiteCommand countCmd = new SQLiteCommand(countQuery, connection))
                {
                    int count = Convert.ToInt32(countCmd.ExecuteScalar());

                    if (count >= maxRecordCount)
                    {
                        int recordsToDelete = count - maxRecordCount;
                        string deleteQuery = $"DELETE FROM Texts WHERE ID IN (SELECT ID FROM Texts LIMIT {recordsToDelete})";
                        using (SQLiteCommand deleteCmd = new SQLiteCommand(deleteQuery, connection))
                        {
                            deleteCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string checkQuery = "SELECT COUNT(*) FROM Texts WHERE GeneratedText = @GeneratedText";
                using (SQLiteCommand checkCmd = new SQLiteCommand(checkQuery, connection))
                {
                    checkCmd.Parameters.AddWithValue("@GeneratedText", richTextBox.Text);
                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count == 0)
                    {
                        string insertQuery = "INSERT INTO Texts (GeneratedText) VALUES (@GeneratedText)";
                        using (SQLiteCommand cmd = new SQLiteCommand(insertQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@GeneratedText", richTextBox.Text);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        private void ClearDatabase()
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string deleteQuery = "DELETE FROM Texts";
                using (SQLiteCommand cmd = new SQLiteCommand(deleteQuery, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
    public class ImageManager
    {
        private PictureBox pictureBox1;
        private OpenAIService openAIService;

        public ImageManager(PictureBox pictureBox)
        {
            pictureBox1 = pictureBox;
            var apiKey = "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";//Тот же ключ, поскольку DALL-E использует тот же счёт что и ChatGPT.
            openAIService = new OpenAIService(new OpenAiOptions
            {
                ApiKey = apiKey
            });
        }
        public async Task GenerateImage(string theme, string description)
        {
            pictureBox1.Cursor = Cursors.WaitCursor;
            string prompt = $"{theme}\n{description}";

            var imageResult = await openAIService.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = prompt,
                N = 1,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                User = "TestUser"
            });
            
            if (imageResult.Successful)
            {
                Console.WriteLine(string.Join("\n", imageResult.Results.Select(r => r.Url)));
                if (imageResult.Results.Count > 0)
                {
                    string imageUrl = imageResult.Results[0].Url;
                    try
                    {
                        using (WebClient client = new WebClient())
                        {
                            byte[] imageData = client.DownloadData(imageUrl);
                            using (MemoryStream stream = new MemoryStream(imageData))
                            {
                                pictureBox1.Image = Image.FromStream(stream);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при загрузке и отображении изображения: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    pictureBox1.Cursor = Cursors.Default;
                }
            }
            else
            {
                if (imageResult.Error == null)
                {
                    throw new Exception("Неизвестная ошибка");
                }
                Console.WriteLine($"{imageResult.Error.Code}: {imageResult.Error.Message}");
                pictureBox1.Cursor = Cursors.Default;
            }
        }
    }
    public class TextToSpeechManager
    {
        private SpeechSynthesizer speechSynthesizer;

        public TextToSpeechManager()
        {
            speechSynthesizer = new SpeechSynthesizer();
        }

        public void SpeakText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                speechSynthesizer.SpeakAsync(text);
            }
        }

        public void StopSpeaking()
        {
            speechSynthesizer.SpeakAsyncCancelAll();
        }
    }

}
