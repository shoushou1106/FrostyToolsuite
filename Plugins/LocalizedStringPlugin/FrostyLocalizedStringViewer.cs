using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LocalizedStringPlugin
{
    [TemplatePart(Name = PART_ExportButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_LocalizedString, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    class FrostyLocalizedStringViewer : FrostyBaseEditor
    {
        public override ImageSource Icon => LocalizedStringViewerMenuExtension.imageSource;
        public ILocalizedStringDatabase db => LocalizedStringDatabase.Current;

        private const string PART_ExportButton = "PART_ExportButton";
        private const string PART_ImportButton = "PART_ImportButton";
        private const string PART_ExportLogButton = "PART_ExportLogButton";
        private const string PART_AddStringButton = "PART_AddStringButton";
        private const string PART_LocalizedString = "PART_LocalizedString";
        private const string PART_LocalizedStringHash = "PART_LocalizedStringHash";
        private const string PART_BulkReplaceButton = "PART_BulkReplaceButton";
        private const string PART_Refresh = "PART_Refresh";

        private const string PART_FilterText = "PART_FilterText";
        private const string PART_FilterStringID = "PART_FilterStringID";
        private const string PART_FilterType = "PART_FilterType";
        private const string PART_StringIdList = "PART_StringIdList";

        private const string PART_UpdateCurrentStringButton = "PART_UpdateCurrentStringButton";
        private const string PART_CopyCurrentStringButton = "PART_CopyCurrentStringButton";
        private const string PART_PasteCurrentStringButton = "PART_PasteCurrentStringButton";
        private const string PART_RevertCurrentStringButton = "PART_RevertCurrentStringButton";

        private TextBox tbLocalizedString;
        private TextBox tbLocalizedStringHash;
        private Button btnExport;
        private Button btnImport;
        private Button btnLogExport;
        private Button btnAddString;
        private Button btnBulkReplace;
        private Button btnUpdateCurrentString;
        private Button btnCopyString;
        private Button btnPasteString;
        private Button btnRemoveString;
        private Button refresh;

        private TextBox tbFilter;
        private string CurrentFilterText;
        private TextBox tbFilterStringID;
        private string CurrentFilterstringID;
        private ComboBox ComboFilterType;
        private ListBox stringIdListBox;
        private string ListBoxSelectedString;

        private List<uint> stringIds = new List<uint>();
        private List<string> stringIDListUnfiltered = new List<string>();
        private int currentIndex = 0;
        private bool firstTimeLoad = true;
        private ILogger logger;
        private Random rand = new Random();

        static FrostyLocalizedStringViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyLocalizedStringViewer), new FrameworkPropertyMetadata(typeof(FrostyLocalizedStringViewer)));
        }

        public FrostyLocalizedStringViewer(ILogger inLogger)
        {
            logger = inLogger;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            stringIdListBox = GetTemplateChild(PART_StringIdList) as ListBox;
            stringIDListUnfiltered = new List<string>();

            tbLocalizedString = GetTemplateChild(PART_LocalizedString) as TextBox;
            tbLocalizedStringHash = GetTemplateChild(PART_LocalizedStringHash) as TextBox;
            tbFilter = GetTemplateChild(PART_FilterText) as TextBox;
            tbFilter.LostFocus += Filter_LostFocus;
            tbFilter.KeyDown += Filter_KeyDown;
            CurrentFilterText = "";
            tbFilterStringID = GetTemplateChild(PART_FilterStringID) as TextBox;
            tbFilterStringID.LostFocus += Filter_LostFocus;
            tbFilterStringID.KeyDown += Filter_KeyDown;
            CurrentFilterstringID = "";
            ComboFilterType = GetTemplateChild(PART_FilterType) as ComboBox;
            ComboFilterType.Items.Add("Display all strings");
            ComboFilterType.Items.Add("Show only modified strings");
            ComboFilterType.Items.Add("Show only unmodified strings");
            ComboFilterType.SelectedIndex = 0;
            ComboFilterType.SelectionChanged += ComboFilterType_SelectionChanged;

            btnUpdateCurrentString = GetTemplateChild(PART_UpdateCurrentStringButton) as Button;
            btnUpdateCurrentString.IsEnabled = false;
            btnCopyString = GetTemplateChild(PART_CopyCurrentStringButton) as Button;
            btnPasteString = GetTemplateChild(PART_PasteCurrentStringButton) as Button;
            btnRemoveString = GetTemplateChild(PART_RevertCurrentStringButton) as Button;
            btnRemoveString.Click += BtnRemoveString_Click;
            btnRemoveString.IsEnabled = false;
            btnPasteString.IsEnabled = false;
            refresh = GetTemplateChild(PART_Refresh) as Button;
            refresh.Click += Refresh_Click;

            btnExport = GetTemplateChild(PART_ExportButton) as Button;
            btnImport = GetTemplateChild(PART_ImportButton) as Button;
            btnLogExport = GetTemplateChild(PART_ExportLogButton) as Button;
            btnAddString = GetTemplateChild(PART_AddStringButton) as Button;
            btnBulkReplace = GetTemplateChild(PART_BulkReplaceButton) as Button;
            btnLogExport.Click += PART_ExportLogButton_Click;

            stringIdListBox.SelectionChanged += stringIdListbox_SelectionChanged;
            btnExport.Click += PART_ExportButton_Click;
            btnImport.Click += BtnImport_Click;
            btnAddString.Click += BtnAddString_Click;
            btnBulkReplace.Click += PART_BulkReplaceButton_Click;

            btnUpdateCurrentString.Click += PART_UpdateCurrentStringButton_Click;
            btnCopyString.Click += PART_CopyStringButton_Click;
            btnPasteString.Click += PART_PasteStringButton_Click;
            tbLocalizedString.KeyDown += TbLocalizedString_KeyDown;
            tbLocalizedString.TextChanged += TbLocalizedString_TextChanged;

            Loaded += FrostyLocalizedStringViewer_Loaded;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            FrostyTaskWindow.Show("Loading Strings", "", (task) =>
            {
                stringIds = db.EnumerateStrings().Distinct().ToList();
                stringIds.Sort();
            });

            if (stringIds.Count == 0)
            {
                btnExport.IsEnabled = false;
                return;
            }

            FillStringIDs(stringIds);
            RemakeList();
        }

        private void BtnRemoveString_Click(object sender, RoutedEventArgs e)
        {
            int Unfilteredidx = stringIDListUnfiltered.IndexOf((string)stringIdListBox.SelectedItem);
            int selected = stringIdListBox.SelectedIndex;
            uint stringId = stringIds[Unfilteredidx];
            db.RevertString(stringId);
            stringIDListUnfiltered[Unfilteredidx] = stringId.ToString("X8") + " - " + db.GetString(stringId);
            stringIdListBox.Items[selected] = stringId.ToString("X8") + " - " + db.GetString(stringId);
            if (ComboFilterType.SelectedIndex == 1)
            {
                stringIdListBox.Items.RemoveAt(selected);
                stringIdListBox.SelectedItem = -1;
            }
            btnRemoveString.IsEnabled = false;
        }

        private void TbLocalizedString_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                int pos = tbLocalizedString.CaretIndex;
                tbLocalizedString.Text = tbLocalizedString.Text.Substring(0, pos) + "\n" + tbLocalizedString.Text.Substring(pos, tbLocalizedString.Text.Length - pos);
                try
                {
                    tbLocalizedString.CaretIndex = pos + 1;
                }
                catch
                {
                    tbLocalizedString.CaretIndex = pos;
                }
            }
        }

        private void TbLocalizedString_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (stringIdListBox.SelectedItem != null)
            {
                if (tbLocalizedString.Text != db.GetString(stringIds[stringIDListUnfiltered.IndexOf(ListBoxSelectedString)]))
                {
                    btnUpdateCurrentString.IsEnabled = true;
                }
                else
                {
                    btnUpdateCurrentString.IsEnabled = false;
                }
            }
        }

        private void PART_PasteStringButton_Click(object sender, RoutedEventArgs e)
        {
            if (StringToCopy != null)
            {
                tbLocalizedString.Text = StringToCopy;
                if (tbLocalizedString.Text != db.GetString(stringIds[stringIDListUnfiltered.IndexOf(ListBoxSelectedString)]))
                {
                    btnUpdateCurrentString.IsEnabled = true;
                }
                else
                {
                    btnUpdateCurrentString.IsEnabled = false;
                }
            }
        }

        private void PART_CopyStringButton_Click(object sender, RoutedEventArgs e)
        {
            if (tbLocalizedString.Text != null)
            {
                StringToCopy = tbLocalizedString.Text;
                btnPasteString.IsEnabled = true;
            }
        }

        private string StringToCopy = null;

        private void PART_UpdateCurrentStringButton_Click(object sender, RoutedEventArgs e)
        {
            btnUpdateCurrentString.IsEnabled = false;

            if (tbLocalizedString.Text != null)
            {
                int Unfilteredidx = stringIDListUnfiltered.IndexOf((string)stringIdListBox.SelectedItem);
                int selected = stringIdListBox.SelectedIndex;
                uint stringId = stringIds[Unfilteredidx];
                db.SetString(stringId, tbLocalizedString.Text);
                stringIDListUnfiltered[Unfilteredidx] = stringId.ToString("X8") + " - " + db.GetString(stringId);
                stringIdListBox.Items[selected] = stringId.ToString("X8") + " - " + db.GetString(stringId);
                if (ComboFilterType.SelectedIndex == 2)
                {
                    stringIdListBox.Items.RemoveAt(selected);
                    stringIdListBox.SelectedItem = -1;
                }
                else if (!tbLocalizedString.Text.Contains(CurrentFilterText))
                {
                    stringIdListBox.SelectedItem = -1;
                    FilterStrings();
                }

            }

        }

        private void PART_BulkReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            db.BulkReplaceWindow();
            FillStringIDs(stringIds);
            RemakeList();
        }

        private void BtnAddString_Click(object sender, RoutedEventArgs e)
        {
            db.AddStringWindow();
            foreach (uint stringid in db.EnumerateStrings())
            {
                if (!stringIds.Contains(stringid))
                {
                    stringIds.Add(stringid);
                    stringIds.Sort();
                }
            }
            FillStringIDs(stringIds);
            RemakeList();
            if (rand.Next(0, 99) == 23)
            {
                FrostyMessageBox.Show(Encoding.ASCII.GetString(new byte[] { 0x42, 0x61, 0x6c, 0x64, 0x75, 0x72, 0x20, 0x69, 0x73, 0x20, 0x62, 0x6c, 0x65, 0x73, 0x73, 0x65, 0x64, 0x20, 0x77, 0x69, 0x74, 0x68, 0x20, 0x69, 0x6e, 0x76, 0x75, 0x6c, 0x6e, 0x65, 0x72, 0x61, 0x62, 0x69, 0x6c, 0x69, 0x74, 0x79, 0x20, 0x74, 0x6f, 0x20, 0x61, 0x6c, 0x6c, 0x20, 0x74, 0x68, 0x72, 0x65, 0x61, 0x74, 0x73, 0x2c, 0x20, 0x70, 0x68, 0x79, 0x73, 0x69, 0x63, 0x61, 0x6c, 0x20, 0x6f, 0x72, 0x20, 0x6d, 0x61, 0x67, 0x69, 0x63, 0x61, 0x6c, 0x2e }), Encoding.ASCII.GetString(new byte[] { 0x48, 0x65, 0x61, 0x64 }));
            }
        }

        private void FrostyLocalizedStringViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstTimeLoad)
            {
                FrostyTaskWindow.Show("Loading Strings", "", (task) =>
                {
                    stringIds = db.EnumerateStrings().Distinct().ToList();
                    stringIds.Sort();
                });
                firstTimeLoad = false;
            }

            if (stringIds.Count == 0)
            {
                btnExport.IsEnabled = false;
                return;
            }

            FillStringIDs(stringIds);
            RemakeList();
        }

        private void FillStringIDs(List<uint> stringIDs)
        {
            stringIdListBox.Items.Clear();
            stringIDListUnfiltered.Clear();
            foreach (uint stringId in stringIds)
            {
                stringIdListBox.Items.Add(stringId.ToString("X8") + " - " + db.GetString(stringId));
                stringIDListUnfiltered.Add(stringId.ToString("X8") + " - " + db.GetString(stringId));
            }
        }

        private void stringIdListbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnUpdateCurrentString.IsEnabled = false;
            if (stringIdListBox.SelectedItem != null)
            {
                btnCopyString.IsEnabled = true;
                if (StringToCopy != null)
                {
                    btnPasteString.IsEnabled = true;
                }
                else
                {
                    btnPasteString.IsEnabled = false;
                }
                ListBoxSelectedString = ((string)stringIdListBox.SelectedItem);
                uint stringID = stringIds[stringIDListUnfiltered.IndexOf(ListBoxSelectedString)];
                if (db.isStringEdited(stringID))
                {
                    btnRemoveString.IsEnabled = true;
                }
                else
                {
                    btnRemoveString.IsEnabled = false;
                }
                PopulateLocalizedString(stringID.ToString("X8"));
            }
            else
            {
                tbLocalizedString.Text = "";
                tbLocalizedStringHash.Text = "";
                btnCopyString.IsEnabled = false;
                btnPasteString.IsEnabled = false;
                btnRemoveString.IsEnabled = false;
            }
        }

        private void PopulateLocalizedString(string stringText)
        {
            stringText = stringText.ToLower();

            if (stringText.StartsWith("id_"))
            {
                tbLocalizedString.Text = db.GetString(stringText);
                tbLocalizedStringHash.Text = stringText;
                return;
            }

            if (!uint.TryParse(stringText, System.Globalization.NumberStyles.HexNumber, null, out uint value))
            {
                //tbStringId.Text = "";
                tbLocalizedString.Text = "";
                tbLocalizedStringHash.Text = "";
                return;
            }
            tbLocalizedStringHash.Text = value.ToString("X8");
            tbLocalizedString.Text = db.GetString(value);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (stringIds.Count == 0)
                return;

            currentIndex--;
            if (currentIndex < 0)
                currentIndex = stringIds.Count - 1;
            PopulateLocalizedString(stringIds[currentIndex].ToString("X8"));
        }

        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            if (stringIds.Count == 0)
                return;

            currentIndex++;
            if (currentIndex > stringIds.Count - 1)
                currentIndex = 0;
            PopulateLocalizedString(stringIds[currentIndex].ToString("X8"));
        }

        private uint HashStringId(string stringId)
        {
            uint result = 0xFFFFFFFF;
            for (int i = 0; i < stringId.Length; i++)
                result = stringId[i] + 33 * result;
            return result;
        }

        private void Filter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckFilterStrings();
            }
        }

        private void Filter_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckFilterStrings();
        }

        private void CheckFilterStrings()
        {
            if (CurrentFilterstringID != tbFilterStringID.Text || CurrentFilterText != tbFilter.Text)
            {
                FilterStrings();
            }
        }

        private void FilterStrings()
        {
            stringIdListBox.Items.Filter = new Predicate<object>((object a) => ((((string)a).Substring(0, 8).ToLower().Contains(tbFilterStringID.Text.ToLower())) & (((string)a).Substring(10).ToLower().Contains(tbFilter.Text.ToLower()))));
            CurrentFilterstringID = tbFilterStringID.Text;
            CurrentFilterText = tbFilter.Text;
        }

        private void ComboFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemakeList();
        }

        private void RemakeList()
        {
            btnCopyString.IsEnabled = false;
            btnPasteString.IsEnabled = false;
            btnUpdateCurrentString.IsEnabled = false;
            CurrentFilterstringID = "";
            CurrentFilterText = "";
            stringIds.Clear();
            if (ComboFilterType.SelectedIndex == 0)
            {
                stringIds = db.EnumerateStrings().Distinct().ToList();
            }
            else if (ComboFilterType.SelectedIndex == 1)
            {
                stringIds = db.EnumerateModifiedStrings().Distinct().ToList();
            }
            else if (ComboFilterType.SelectedIndex == 2)
            {
                stringIds = db.EnumerateStrings().Distinct().Except(db.EnumerateModifiedStrings().Distinct().ToList()).ToList();
            }
            stringIds.Sort();
            FillStringIDs(stringIds);

            CheckFilterStrings();

            if (ListBoxSelectedString != null)
            {
                if (stringIdListBox.Items.Contains(ListBoxSelectedString))
                {
                    stringIdListBox.SelectedIndex = stringIdListBox.Items.IndexOf(ListBoxSelectedString);
                }
            }
        }

        public struct OptStr
        {
            public string Key;
            public string Eng;
            public string Zh;
            public List<string> Path;
            public List<string> Guess;
            public string Opt
            {
                get
                {
                    //#正式使用时请删除前三行。Please remove the first 3 lines in production.
                    //# 键值,原文,译文,上下文（可选）
                    //# Key,Source,Translation,Context(optional)
                    //key_apple,apple,苹果,"A common, round fruit produced by the tree Malus domestica, cultivated in temperate climates."
                    //key_pear,pear,梨
                    //key_peach, peach, 桃子
                    //key_peach_etymology,"The scientific name persica, along with the word ""peach"" itself and its cognates in many European languages, derives from an early European belief that peaches were native to Persia (modern-day Iran).",
                    //key_potato,potato,马铃薯
                    //key_peas, peas, 豌豆
                    //key_green_bean,green bean, 青豆

                    StringBuilder sb = new StringBuilder();
                    sb.Append(Key);
                    sb.Append(",\"");
                    if (Eng.Contains('\"'))
                        sb.Append("稍后上传");
                    else
                        sb.Append(Eng);
                    sb.Append("\",\"\",\"");

                    var list = Path.Distinct().ToList();
                    sb.AppendLine("路径: ");
                    foreach (string str in list)
                        sb.AppendLine(str);

                    list = Guess.Distinct().ToList();
                    sb.Append("猜测: ");
                    foreach (string str in list)
                        sb.Append(str + "; ");

                    sb.AppendLine();
                    sb.AppendLine("繁体中文: ");
                    sb.Append(Zh);

                    sb.Append("\"");

                    return sb.ToString();
                }
            }
        }

        public Dictionary<string, OptStr> EngStr { get; set; }

        public List<string> GuessTag(string path)
        {
            path = path.ToLower();
            List<string> tags = new List<string>();
            if (path.Contains("_pvz/ui/customization"))
                tags.Add("装饰");
            if (path.Contains("_pvz/ui/uiconsumablemetadata"))
                tags.Add("消耗品");
            if (path.Contains("packcardmetadata"))
                tags.Add("组合包");
            if (path.Contains("online/persistence/pvzawards"))
                tags.Add("任务");
            if (path.Contains("online/persistence/scoring/pvzscoring"))
                tags.Add("得分");
            if (path.Contains("gameplay/ai/ai_templates"))
                tags.Add("AI名称");
            if (path.Contains("BossSpeech"))
                tags.Add("演讲");
            if (path.StartsWith("_pvz"))
                tags.Add("GW1");
            if (path.StartsWith("ui"))
                tags.Add("用户界面");
            if (path.StartsWith("_pvz/ui"))
                tags.Add("用户界面");
            if (path.StartsWith("online"))
                tags.Add("在线");
            if (path.StartsWith("gameplay"))
                tags.Add("玩法");
            if (path.StartsWith("levels"))
                tags.Add("地图");
            if (path.StartsWith("worlds"))
                tags.Add("地图");
            if (path.StartsWith("sound"))
                tags.Add("声音");

            return tags;
        }

        private void PART_ExportButton_Click(object sender, RoutedEventArgs e)
        {
            EngStr = new Dictionary<string, OptStr>();

            FrostyTaskWindow.Show("Exporting Localized Strings Usage", "", (task) =>
            {
                uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
                uint idx = 0;

                foreach (uint stringId in stringIds)
                {
                    OptStr str = new OptStr();
                    str.Key = stringId.ToString("X8");
                    str.Eng = db.GetString(stringId);
                    str.Path = new List<string>();
                    str.Guess = new List<string>();
                    EngStr.Add(stringId.ToString("X").ToLower(), str);
                }
                foreach (EbxAssetEntry refEntry in App.AssetManager.EnumerateEbx())
                {
                    task.Update("Checking: " + refEntry.Name, (idx++ / (double)totalCount) * 100.0d);
                    EbxAsset refAsset = App.AssetManager.GetEbx(refEntry);
                    List<string> AlreadyDone = new List<string>();
                    foreach (dynamic obj in refAsset.Objects)
                    {
                        if (HasProperty(obj, "StringHash"))
                        {
                            string TempString = obj.StringHash.ToString("X").ToLower();
                            if (EngStr.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
                            {
                                AlreadyDone.Add(TempString);
                                OptStr str = EngStr[TempString];
                                str.Path.Add(refEntry.Name);
                                str.Guess = EngStr[TempString].Guess.Concat(GuessTag(refEntry.Name)).ToList();
                                EngStr[TempString] = str;
                            }
                        }
                        foreach (PropertyInfo pi in obj.GetType().GetProperties())
                        {
                            if (pi.PropertyType == typeof(CString))
                            {
                                string TempString = HashStringId(pi.GetValue(obj)).ToString("X").ToLower();
                                if (EngStr.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
                                {
                                    AlreadyDone.Add(TempString);
                                    OptStr str = EngStr[TempString];
                                    str.Path.Add(refEntry.Name);
                                    str.Guess = EngStr[TempString].Guess.Concat(GuessTag(refEntry.Name)).ToList();
                                    EngStr[TempString] = str;
                                }
                            }
                            else if (pi.PropertyType == typeof(List<CString>))
                            {
                                foreach (CString cst in pi.GetValue(obj))
                                {
                                    string TempString = HashStringId(cst).ToString("X").ToLower();
                                    if (EngStr.ContainsKey(TempString) & !AlreadyDone.Contains(TempString))
                                    {
                                        AlreadyDone.Add(TempString);
                                        OptStr str = EngStr[TempString];
                                        str.Path.Add(refEntry.Name);
                                        str.Guess = EngStr[TempString].Guess.Concat(GuessTag(refEntry.Name)).ToList();
                                        EngStr[TempString] = str;
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Import Localized Strings", "*.csv (CSV File)|*.csv", "LocalizedStrings");
            if (ofd.ShowDialog())
            {
                int modified = 0;
                int added = 0;
                FrostyTaskWindow.Show("Importing Localized Strings", "", (task) =>
                {
                    using (StreamReader reader = new StreamReader(ofd.FileName))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            uint hash = uint.Parse(line.Substring(0, 8), System.Globalization.NumberStyles.HexNumber);
                            string s = line.Substring(10, line.Length - 11);
                            if (stringIds.Contains(hash) && s != db.GetString(hash))
                            {
                                db.SetString(hash, s);
                                modified++;
                            }
                            else
                            {
                                db.SetString(hash, s);
                                added++;
                            }
                        }
                    }
                });
                Refresh_Click(sender, e);
                logger.Log(string.Format("{0} strings modified and {1} strings added.", modified, added));
            }
        }


        public static bool HasProperty(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName) != null;
        }

        private void PART_ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings Usage List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
            if (sfd.ShowDialog())
            {
                FrostyTaskWindow.Show("Exporting Localized Strings Usage", "", (task) =>
                {
                    uint totalCount = (uint)App.AssetManager.EnumerateEbx().ToList().Count;
                    uint idx = 0;
                    Dictionary<string, string> StringInfo = new Dictionary<string, string>();
                    foreach (uint stringId in stringIds)
                    {
                        OptStr str = EngStr[stringId.ToString("X").ToLower()];
                        str.Zh = db.GetString(stringId);
                        EngStr[stringId.ToString("X").ToLower()] = str;
                    }

                    using (StreamWriter writer = new StreamWriter(sfd.FileName))
                    {
                        foreach (OptStr StringData in EngStr.Values)
                        {
                            writer.WriteLine(StringData.Opt);
                        }
                    }
                });

                App.Logger.Log("Localized strings usage saved to {0}", sfd.FileName);
            }
        }
    }
}
