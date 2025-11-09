using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PairwiseKit;
using PairwiseKit.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PairwiseKit
{
    public class GuiWindow : Window
    {
        // --- UI ---
        private TextBox _specBox;
        private DataGrid _grid;
        private TextBlock _info;

        // конструктор параметров
        private TextBox _paramNameBox, _paramValuesBox;
        private Button _addParamBtn, _removeParamBtn, _clearParamsBtn, _importBtn, _exportBtn;
        private ListBox _paramsList;

        // --- state ---
        private List<Dictionary<string,string>> _rows = new();
        private Spec _spec = new();
        private Dictionary<string, List<string>> _builderParams = new(); // имя параметра -> список значений

        public GuiWindow()
        {
            Title = "Pairwise Kit — GUI";
            Width = 1100; Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { Margin = new Thickness(10) };

            // ---------- Верхняя панель ----------
            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
            var openBtn = new Button { Content = "📂 Открыть spec.yml", Padding = new Thickness(10,5,10,5) };
            var genBtn  = new Button { Content = "▶️ Сгенерировать",    Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            var saveCsvBtn  = new Button { Content = "💾 Сохранить CSV", Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            var saveJsonBtn = new Button { Content = "💾 Сохранить JSON",Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            _info = new TextBlock { Margin = new Thickness(16,0,0,0), VerticalAlignment = VerticalAlignment.Center };

            openBtn.Click += OpenBtn_Click;
            genBtn.Click  += Generate_Click;
            saveCsvBtn.Click  += SaveCsv_Click;
            saveJsonBtn.Click += SaveJson_Click;

            top.Children.Add(openBtn);
            top.Children.Add(genBtn);
            top.Children.Add(saveCsvBtn);
            top.Children.Add(saveJsonBtn);
            top.Children.Add(_info);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            // ---------- Основная область ----------
            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // слева — конструктор + YAML
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // справа — результат

            // ----- левая колонка -----
            var left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // конструктор
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // YAML

            // Конструктор параметров
            var builder = new GroupBox { Header = "Конструктор параметров", Margin = new Thickness(0,0,8,8) };
            var bGrid = new Grid { Margin = new Thickness(8) };
            bGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
            bGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Имя параметра
            bGrid.Children.Add(new TextBlock { Text = "Параметр:", Margin = new Thickness(0,0,8,8), VerticalAlignment = VerticalAlignment.Center });
            _paramNameBox = new TextBox { Margin = new Thickness(0,0,0,8) };
            Grid.SetColumn(_paramNameBox, 1);
            bGrid.Children.Add(_paramNameBox);

            // Значения
            var valuesLbl = new TextBlock { Text = "Значения (через запятую):", Margin = new Thickness(0,0,8,8), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(valuesLbl, 1);
            bGrid.Children.Add(valuesLbl);
            _paramValuesBox = new TextBox { Margin = new Thickness(0,0,0,8) };
            Grid.SetRow(_paramValuesBox, 1); Grid.SetColumn(_paramValuesBox, 1);
            bGrid.Children.Add(_paramValuesBox);

            // Кнопки добавления/удаления
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
            _addParamBtn = new Button { Content = "Добавить/Обновить", Padding = new Thickness(10,5,10,5) };
            _removeParamBtn = new Button { Content = "Удалить выбранный", Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            _clearParamsBtn = new Button { Content = "Очистить все", Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            buttonsPanel.Children.Add(_addParamBtn);
            buttonsPanel.Children.Add(_removeParamBtn);
            buttonsPanel.Children.Add(_clearParamsBtn);
            Grid.SetRow(buttonsPanel, 2); Grid.SetColumn(buttonsPanel, 1);
            bGrid.Children.Add(buttonsPanel);

            _addParamBtn.Click += AddParam_Click;
            _removeParamBtn.Click += RemoveParam_Click;
            _clearParamsBtn.Click += ClearParams_Click;

            // Список параметров
            var listPanel = new Grid();
            listPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var listLbl = new TextBlock { Text = "Параметры:", Margin = new Thickness(0,0,8,0) };
            Grid.SetColumnSpan(listLbl, 2);
            listPanel.Children.Add(listLbl);

            _paramsList = new ListBox { Margin = new Thickness(0,20,8,0) };
            Grid.SetColumn(_paramsList, 0);

            var selectedValues = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(selectedValues, 1);

            _paramsList.SelectionChanged += (s,e) =>
            {
                if (_paramsList.SelectedItem is string key && _builderParams.TryGetValue(key, out var vals))
                    selectedValues.Text = string.Join(", ", vals);
                else
                    selectedValues.Text = "";
            };

            listPanel.Children.Add(_paramsList);
            listPanel.Children.Add(selectedValues);
            Grid.SetRow(listPanel, 3); Grid.SetColumnSpan(listPanel, 2);
            bGrid.Children.Add(listPanel);

            // Импорт/Экспорт YAML
            var syncPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _importBtn = new Button { Content = "⬅ Импорт из YAML", Padding = new Thickness(10,5,10,5) };
            _exportBtn = new Button { Content = "Экспорт в YAML ➡", Padding = new Thickness(10,5,10,5), Margin = new Thickness(8,0,0,0) };
            syncPanel.Children.Add(_importBtn);
            syncPanel.Children.Add(_exportBtn);
            Grid.SetRow(syncPanel, 4); Grid.SetColumnSpan(syncPanel, 2);
            bGrid.Children.Add(syncPanel);

            _importBtn.Click += ImportFromYaml_Click;
            _exportBtn.Click += ExportToYaml_Click;

            builder.Content = bGrid;
            left.Children.Add(builder);

            // YAML поле
            _specBox = new TextBox
            {
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Text = "" // пусто — редактируй или наполни экспортом
            };
            Grid.SetRow(_specBox, 1);
            left.Children.Add(_specBox);

            // ----- правая колонка -----
            var right = new GroupBox { Header = "Результат", Margin = new Thickness(8,0,0,0) };
            _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(0) };
            right.Content = _grid;

            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            main.Children.Add(left);
            main.Children.Add(right);

            root.Children.Add(main);
            Content = root;

            RefreshParamsList();
        }

        // ================== Конструктор параметров ==================
        private void AddParam_Click(object? sender, RoutedEventArgs e)
        {
            var name = (_paramNameBox.Text ?? "").Trim();
            var values = (_paramValuesBox.Text ?? "").Split(',')
                          .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите имя параметра.");
                return;
            }
            if (values.Count == 0)
            {
                MessageBox.Show("Введите хотя бы одно значение (через запятую).");
                return;
            }

            _builderParams[name] = values;
            RefreshParamsList();

            // 🔹 очищаем поля после добавления — как просил(а)
            _paramNameBox.Text = string.Empty;
            _paramValuesBox.Text = string.Empty;
            _paramNameBox.Focus();
        }

        private void RemoveParam_Click(object? sender, RoutedEventArgs e)
        {
            if (_paramsList.SelectedItem is string key && _builderParams.Remove(key))
                RefreshParamsList();
        }

        private void ClearParams_Click(object? sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить все параметры конструктора?", "Подтвердите", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _builderParams.Clear();
                RefreshParamsList();
            }
        }

        private void ImportFromYaml_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var deser = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                var spec = deser.Deserialize<Spec>(_specBox.Text) ?? new Spec();
                _builderParams = spec.Parameters ?? new();
                RefreshParamsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось импортировать из YAML: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToYaml_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // forbid/require оставляем пустыми — их можно дописать вручную в YAML-поле
                var spec = new Spec { Parameters = new Dictionary<string, List<string>>(_builderParams), Forbid = new(), Require = new() };
                var ser = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                _specBox.Text = ser.Serialize(spec);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось экспортировать в YAML: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshParamsList()
        {
            _paramsList.ItemsSource = null;
            _paramsList.ItemsSource = _builderParams.Keys.OrderBy(k => k).ToList();
        }

        // ================== Генерация/сохранение ==================
        private void OpenBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "YAML (*.yml;*.yaml)|*.yml;*.yaml|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) _specBox.Text = File.ReadAllText(dlg.FileName);
        }

        private void Generate_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 1) читаем forbid/require из YAML, но параметры берём из конструктора, если они заданы
                var deser = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                var parsed = deser.Deserialize<Spec>(_specBox.Text ?? string.Empty) ?? new Spec();
                parsed.Parameters ??= new(); parsed.Forbid ??= new(); parsed.Require ??= new();

                var parametersToUse = (_builderParams != null && _builderParams.Count > 0)
                                      ? new Dictionary<string, List<string>>(_builderParams)  // ✅ приоритет конструктору
                                      : parsed.Parameters;

                _spec = new Spec
                {
                    Parameters = parametersToUse,
                    Forbid = parsed.Forbid,     // можно дописать руками в YAML
                    Require = parsed.Require
                };

                _rows = PairwiseGenerator.GeneratePairwise(_spec.Parameters, _spec.Forbid, _spec.Require);

                var headers = _spec.Parameters.Keys.ToList();
                var table = BuildTable(headers, _rows);
                _grid.ItemsSource = table.DefaultView;

                long total = 1;
                foreach (var list in _spec.Parameters.Values)
                    total *= Math.Max(1, list.Count);
                var saved = total > 0 ? 100.0 * (1.0 - (double)_rows.Count / total) : 0.0;
                _info.Text = $"Комбинаций: {total}, тест-кейсов: {_rows.Count}, экономия ≈ {saved:0.#}%";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при разборе YAML или генерации: " + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCsv_Click(object? sender, RoutedEventArgs e)
        {
            if (_rows == null || _rows.Count == 0) { MessageBox.Show("Сначала сгенерируйте набор."); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "cases.csv" };
            if (dlg.ShowDialog() == true)
            {
                var headers = _spec.Parameters.Keys.ToList();
                using var sw = new StreamWriter(dlg.FileName);
                sw.WriteLine(string.Join(",", headers));
                foreach (var r in _rows)
                    sw.WriteLine(string.Join(",", headers.Select(h => CsvEscape(r.TryGetValue(h, out var v) ? v : ""))));
            }
        }

        private void SaveJson_Click(object? sender, RoutedEventArgs e)
        {
            if (_rows == null || _rows.Count == 0) { MessageBox.Show("Сначала сгенерируйте набор."); return; }
            var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "cases.json" };
            if (dlg.ShowDialog() == true)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
            }
        }

        // ================== Helpers ==================
        private static DataTable BuildTable(IEnumerable<string> headers, List<Dictionary<string,string>> rows)
        {
            var dt = new DataTable();
            foreach (var h in headers) dt.Columns.Add(h);
            foreach (var r in rows)
            {
                var dr = dt.NewRow();
                foreach (var h in headers) dr[h] = r.TryGetValue(h, out var v) ? v : "";
                dt.Rows.Add(dr);
            }
            return dt;
        }

        private static string CsvEscape(string s)
        {
            if (s.Contains('"') || s.Contains(',') || s.Contains('\\'))
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