using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using PairwiseKit;
using PairwiseKit.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PairwiseKit
{
    public class GuiWindow : Window
    {
        private TextBox _specBox;
        private DataGrid _grid;
        private TextBlock _info;
        private Button _genBtn;
        private Button _saveCsvBtn;

        public GuiWindow()
        {
            Title = "Pairwise Kit — GUI";
            Width = 900;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { Margin = new Thickness(10) };

            // Верхняя панель с кнопками
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var openBtn = new Button { Content = "📂 Открыть spec.yml", Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(10, 5, 10, 5) };
            _genBtn = new Button { Content = "▶️ Сгенерировать", Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(10, 5, 10, 5) };
            _saveCsvBtn = new Button { Content = "💾 Сохранить CSV", Padding = new Thickness(10, 5, 10, 5), IsEnabled = false };
            _info = new TextBlock { Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            openBtn.Click += OpenBtn_Click;
            _genBtn.Click += Generate_Click;
            _saveCsvBtn.Click += SaveCsv_Click;

            topPanel.Children.Add(openBtn);
            topPanel.Children.Add(_genBtn);
            topPanel.Children.Add(_saveCsvBtn);
            topPanel.Children.Add(_info);
            DockPanel.SetDock(topPanel, Dock.Top);
            root.Children.Add(topPanel);

            // Основная область — YAML слева и таблица справа
            var gridMain = new Grid();
            gridMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            _specBox = new TextBox
            {
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Text = @"parameters:
  Browser: [Chrome, Firefox, Safari, Opera, Yandex]
  OS: [Windows, macOS, Linux, Avrora]
  Auth: [SAML, Basic, OAuth]
forbid:
  - { Browser: ""Safari"", OS: ""Windows"" }
  - { Auth: ""SAML"", OS: ""Linux"" }
require: []"
            };

            _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10, 0, 0, 0) };

            Grid.SetColumn(_specBox, 0);
            Grid.SetColumn(_grid, 1);
            gridMain.Children.Add(_specBox);
            gridMain.Children.Add(_grid);

            root.Children.Add(gridMain);
            Content = root;
        }

        private void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "YAML files (*.yml;*.yaml)|*.yml;*.yaml|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _specBox.Text = File.ReadAllText(dlg.FileName);
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var deser = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var spec = deser.Deserialize<Spec>(_specBox.Text) ?? new Spec();
                spec.Parameters ??= new();
                spec.Forbid ??= new();
                spec.Require ??= new();

                var rows = PairwiseGenerator.GeneratePairwise(spec.Parameters, spec.Forbid, spec.Require);
                _grid.ItemsSource = rows;
                _saveCsvBtn.IsEnabled = rows.Count > 0;

                // считаем экономию
                long total = 1;
                foreach (var list in spec.Parameters.Values)
                    total *= Math.Max(1, list.Count);
                double saved = 100.0 * (1.0 - (double)rows.Count / total);
                _info.Text = $"Комбинаций: {total}, тест-кейсов: {rows.Count}, экономия ≈ {saved:0.#}%";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка генерации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_grid.ItemsSource is not IEnumerable<Dictionary<string, string>> rows) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "cases.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                var list = rows.ToList();
                if (list.Count == 0) return;
                var headers = list[0].Keys.ToList();
                using var sw = new StreamWriter(dlg.FileName);
                sw.WriteLine(string.Join(",", headers));
                foreach (var r in list)
                    sw.WriteLine(string.Join(",", headers.Select(h => CsvEscape(r[h]))));
            }
        }

        private static string CsvEscape(string s)
        {
            if (s.Contains('"') || s.Contains(','))
            {
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }
            return s;
        }

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Run(new GuiWindow());
        }
    }
}