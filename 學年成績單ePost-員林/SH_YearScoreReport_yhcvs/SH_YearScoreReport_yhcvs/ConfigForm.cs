﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using K12.Data;
using System.IO;
using FISCA.Data;

namespace SH_YearScoreReport_yhcvs
{
    public partial class ConfigForm : FISCA.Presentation.Controls.BaseForm
    {      
        private FISCA.UDT.AccessHelper _AccessHelper = new FISCA.UDT.AccessHelper();

        private List<TagConfigRecord> _TagConfigRecords = new List<TagConfigRecord>();
        private List<Configure> _Configures = new List<Configure>();
        private string _DefalutSchoolYear = "";
        private string _DefaultSemester = "";

        public ConfigForm()
        {
            InitializeComponent();
            
            BackgroundWorker bkw = new BackgroundWorker();
            bkw.DoWork += delegate
            {
                bkw.ReportProgress(1);
                //預設學年度學期
                _DefalutSchoolYear = "" + K12.Data.School.DefaultSchoolYear;
                _DefaultSemester = "" + K12.Data.School.DefaultSemester;

                _TagConfigRecords = K12.Data.TagConfig.SelectByCategory(TagCategory.Student);
                bkw.ReportProgress(20);
                //學生類別清單
                
                #region 整理對應科目

                List<string> SubjectNameList = new List<string>();
                bkw.ReportProgress(30);

                QueryHelper qh = new QueryHelper();

                string strSQ = @"SELECT DISTINCT tmp.subject
FROM xpath_table( 'id',
				'''<root>''||score_info||''</root>''',
				'sems_subj_score',
				'/root/SemesterSubjectScoreInfo/Subject/@科目',
				'ref_student_id IN ( select student.id from student 
									INNER JOIN class ON student.ref_class_id = class.id 
									WHERE student.status=1 AND sems_subj_score="+_DefalutSchoolYear+@")'
				) 
AS tmp(id int, subject varchar(200))";
                DataTable dt = qh.Select(strSQ);
                foreach (DataRow dr in dt.Rows)
                    SubjectNameList.Add(dr[0].ToString());
                
                bkw.ReportProgress(60);

                SubjectNameList.Sort(new StringComparer("國文"
                                    , "英文"
                                    , "數學"
                                    , "理化"
                                    , "生物"
                                    , "社會"
                                    , "物理"
                                    , "化學"
                                    , "歷史"
                                    , "地理"
                                    , "公民")
                    );

                bkw.ReportProgress(70);
           
                #endregion
                bkw.ReportProgress(80);
                _Configures = _AccessHelper.Select<Configure>();
                bkw.ReportProgress(100);

            };
            bkw.WorkerReportsProgress = true;
            bkw.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                circularProgress1.Value = e.ProgressPercentage;
            };
            bkw.RunWorkerCompleted += delegate
            {
                cboConfigure.Items.Clear();
                foreach (var item in _Configures)
                {
                    cboConfigure.Items.Add(item);
                }
                cboConfigure.Items.Add(new Configure() { Name = "新增" });

                iptSchoolYear.Value = int.Parse(School.DefaultSchoolYear);
                
                List<string> prefix = new List<string>();
                List<string> tag = new List<string>();
                foreach (var item in _TagConfigRecords)
                {
                    if (item.Prefix != "")
                    {
                        if (!prefix.Contains(item.Prefix))
                            prefix.Add(item.Prefix);
                    }
                    else
                    {
                        tag.Add(item.Name);
                    }
                }
                cboRankRilter.Items.Clear();
                cboTagRank1.Items.Clear();
                cboTagRank2.Items.Clear();
                cboRankRilter.Items.Add("");
                cboTagRank1.Items.Add("");
                cboTagRank2.Items.Add("");
                foreach (var s in prefix)
                {
                    cboRankRilter.Items.Add("[" + s + "]");
                    cboTagRank1.Items.Add("[" + s + "]");
                    cboTagRank2.Items.Add("[" + s + "]");
                }
                foreach (var s in tag)
                {
                    cboRankRilter.Items.Add(s);
                    cboTagRank1.Items.Add(s);
                    cboTagRank2.Items.Add(s);
                }
                circularProgress1.Hide();
                if (_Configures.Count > 0)
                {
                    cboConfigure.SelectedIndex = 0;
                }
                else
                {
                    cboConfigure.SelectedIndex = -1;
                }
            };
            bkw.RunWorkerAsync();
        }

        public Configure Configure { get; private set; }

        private void btnPrint_Click(object sender, EventArgs e)
        {            
            SaveTemplate(null, null);
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
     
        private void cboConfigure_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboConfigure.SelectedIndex == cboConfigure.Items.Count - 1)
            {
                //新增
                btnSaveConfig.Enabled = btnPrint.Enabled = false;
                NewConfigure dialog = new NewConfigure();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Configure = new Configure();
                    Configure.Name = dialog.ConfigName;
                    Configure.Template = dialog.Template;
                    Configure.SubjectLimit = dialog.SubjectLimit;
                    Configure.SchoolYear = _DefalutSchoolYear;
                    Configure.Semester = _DefaultSemester;
                    
                    _Configures.Add(Configure);
                    cboConfigure.Items.Insert(cboConfigure.SelectedIndex, Configure);
                    cboConfigure.SelectedIndex = cboConfigure.SelectedIndex - 1;
                    Configure.WithSchoolYearScore = dialog.WithSchoolYearScore;
                    Configure.ExportEpost = true;
                    Configure.WithPrevSemesterScore = dialog.WithPrevSemesterScore;
                    Configure.Encode();
                    Configure.Save();
                }
                else
                {
                    cboConfigure.SelectedIndex = -1;
                }
            }
            else
            {
                if (cboConfigure.SelectedIndex >= 0)
                {                    
                    btnSaveConfig.Enabled = btnPrint.Enabled = true;
                    Configure = _Configures[cboConfigure.SelectedIndex];
                    if (Configure.Template == null)
                        Configure.Decode();

                    int sc;
                    if (int.TryParse(Configure.SchoolYear, out sc))
                        iptSchoolYear.Value = sc;
                    
                   
                    cboRankRilter.Text = Configure.RankFilterTagName;
                    foreach (ListViewItem item in lvSubject.Items)
                    {
                        item.Checked = Configure.PrintSubjectList.Contains(item.Text);
                    }
                    cboTagRank1.Text = Configure.TagRank1TagName;
                    foreach (ListViewItem item in lvSubjTag1.Items)
                    {
                        item.Checked = Configure.TagRank1SubjectList.Contains(item.Text);
                    }
                    cboTagRank2.Text = Configure.TagRank2TagName;
                    foreach (ListViewItem item in lvSubjTag2.Items)
                    {
                        item.Checked = Configure.TagRank2SubjectList.Contains(item.Text);
                    }

                    chkExportEPOST.Checked = Configure.ExportEpost;
                }
                else
                {
                    Configure = null;       
                    cboRankRilter.SelectedIndex = -1;
                    cboTagRank1.SelectedIndex = -1;
                    cboTagRank2.SelectedIndex = -1;
                    chkExportEPOST.Checked = true;
                    foreach (ListViewItem item in lvSubject.Items)
                    {
                        item.Checked = false;
                    }
                    foreach (ListViewItem item in lvSubjTag1.Items)
                    {
                        item.Checked = false;
                    }
                    foreach (ListViewItem item in lvSubjTag2.Items)
                    {
                        item.Checked = false;
                    }
                }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (this.Configure == null) return;
            #region 儲存檔案
            string inputReportName = "學年成績單樣板(" + this.Configure.Name + ")";
            string reportName = inputReportName;

            string path = Path.Combine(System.Windows.Forms.Application.StartupPath, "Reports");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, reportName + ".doc");

            if (File.Exists(path))
            {
                int i = 1;
                while (true)
                {
                    string newPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + (i++) + Path.GetExtension(path);
                    if (!File.Exists(newPath))
                    {
                        path = newPath;
                        break;
                    }
                }
            }

            try
            {
                //document.Save(path, Aspose.Words.SaveFormat.Doc);
                System.IO.FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                this.Configure.Template.Save(stream, Aspose.Words.SaveFormat.Doc);
                //stream.Write(Properties.Resources.個人學期成績單樣板_高中_, 0, Properties.Resources.個人學期成績單樣板_高中_.Length);
                stream.Flush();
                stream.Close();
                System.Diagnostics.Process.Start(path);
            }
            catch
            {
                System.Windows.Forms.SaveFileDialog sd = new System.Windows.Forms.SaveFileDialog();
                sd.Title = "另存新檔";
                sd.FileName = reportName + ".doc";
                sd.Filter = "Word檔案 (*.doc)|*.doc|所有檔案 (*.*)|*.*";
                if (sd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        //document.Save(sd.FileName, Aspose.Words.SaveFormat.Doc);
                        System.IO.FileStream stream = new FileStream(sd.FileName, FileMode.Create, FileAccess.Write);
                        stream.Write(Properties.Resources.員林家商學年成績單樣版, 0, Properties.Resources.員林家商學年成績單樣版.Length);
                        stream.Flush();
                        stream.Close();

                    }
                    catch
                    {
                        FISCA.Presentation.Controls.MsgBox.Show("指定路徑無法存取。", "建立檔案失敗", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            #endregion
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Configure == null) return;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "上傳樣板";
            dialog.Filter = "Word檔案 (*.doc)|*.doc|所有檔案 (*.*)|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    this.Configure.Template = new Aspose.Words.Document(dialog.FileName);
                    List<string> fields = new List<string>(this.Configure.Template.MailMerge.GetFieldNames());
                    this.Configure.SubjectLimit = 0;
                    this.Configure.WithSchoolYearScore = false;                    
                    this.Configure.WithPrevSemesterScore = false;
                    while (fields.Contains("科目名稱" + (this.Configure.SubjectLimit + 1)))
                    {
                        if (fields.Contains("上學期科目原始成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("上學期科目補考成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("上學期科目重修成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("上學期科目手動調整成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("上學期科目學年調整成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("上學期科目成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithPrevSemesterScore = true;
                        if (fields.Contains("學年科目成績" + (this.Configure.SubjectLimit + 1))) this.Configure.WithSchoolYearScore = true;
                        this.Configure.SubjectLimit++;
                    }
                }
                catch
                {
                    MessageBox.Show("樣板開啟失敗");
                }
            }
        }

        private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Configure == null) return;
            if (MessageBox.Show("樣板刪除後將無法回復，確定刪除樣板?", "刪除樣板", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.OK)
            {
                _Configures.Remove(Configure);
                if (Configure.UID != "")
                {
                    Configure.Deleted = true;
                    Configure.Save();
                }
                var conf = Configure;
                cboConfigure.SelectedIndex = -1;
                cboConfigure.Items.Remove(conf);
            }
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Configure == null) return;
            CloneConfigure dialog = new CloneConfigure() { ParentName = Configure.Name };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Configure conf = new Configure();
                conf.Name = dialog.NewConfigureName;                
                conf.PrintSubjectList.AddRange(Configure.PrintSubjectList);
                conf.RankFilterTagList.AddRange(Configure.RankFilterTagList);
                conf.RankFilterTagName = Configure.RankFilterTagName;                
                conf.SchoolYear = Configure.SchoolYear;
                conf.Semester = Configure.Semester;
                conf.SubjectLimit = Configure.SubjectLimit;
                conf.TagRank1SubjectList.AddRange(Configure.TagRank1SubjectList);
                conf.TagRank1TagList.AddRange(Configure.TagRank1TagList);
                conf.TagRank1TagName = Configure.TagRank1TagName;
                conf.TagRank2SubjectList.AddRange(Configure.TagRank2SubjectList);
                conf.TagRank2TagList.AddRange(Configure.TagRank2TagList);
                conf.TagRank2TagName = Configure.TagRank2TagName;
                conf.Template = Configure.Template;
                conf.WithPrevSemesterScore = Configure.WithPrevSemesterScore;
                conf.WithSchoolYearScore = Configure.WithSchoolYearScore;
                conf.ExportEpost = Configure.ExportEpost;
                conf.Encode();
                conf.Save();
                _Configures.Add(conf);
                cboConfigure.Items.Insert(cboConfigure.Items.Count - 1, conf);
                cboConfigure.SelectedIndex = cboConfigure.Items.Count - 2;
            }
        }

        private void SaveTemplate(object sender, EventArgs e)
        {
            if (Configure == null) return;
            Configure.SchoolYear = iptSchoolYear.Value.ToString();
                       
            foreach (ListViewItem item in lvSubject.Items)
            {
                if (item.Checked)
                {
                    if (!Configure.PrintSubjectList.Contains(item.Text))
                        Configure.PrintSubjectList.Add(item.Text);
                }
                else
                {
                    if (Configure.PrintSubjectList.Contains(item.Text))
                        Configure.PrintSubjectList.Remove(item.Text);
                }
            }
            Configure.TagRank1TagName = cboTagRank1.Text;
            Configure.TagRank1TagList.Clear();
            foreach (var item in _TagConfigRecords)
            {
                if (item.Prefix != "")
                {
                    if (cboTagRank1.Text == "[" + item.Prefix + "]")
                        Configure.TagRank1TagList.Add(item.ID);
                }
                else
                {
                    if (cboTagRank1.Text == item.Name)
                        Configure.TagRank1TagList.Add(item.ID);
                }
            }
            foreach (ListViewItem item in lvSubjTag1.Items)
            {
                if (item.Checked)
                {
                    if (!Configure.TagRank1SubjectList.Contains(item.Text))
                        Configure.TagRank1SubjectList.Add(item.Text);
                }
                else
                {
                    if (Configure.TagRank1SubjectList.Contains(item.Text))
                        Configure.TagRank1SubjectList.Remove(item.Text);
                }
            }

            Configure.TagRank2TagName = cboTagRank2.Text;
            Configure.TagRank2TagList.Clear();
            foreach (var item in _TagConfigRecords)
            {
                if (item.Prefix != "")
                {
                    if (cboTagRank2.Text == "[" + item.Prefix + "]")
                        Configure.TagRank2TagList.Add(item.ID);
                }
                else
                {
                    if (cboTagRank2.Text == item.Name)
                        Configure.TagRank2TagList.Add(item.ID);
                }
            }
            foreach (ListViewItem item in lvSubjTag2.Items)
            {
                if (item.Checked)
                {
                    if (!Configure.TagRank2SubjectList.Contains(item.Text))
                        Configure.TagRank2SubjectList.Add(item.Text);
                }
                else
                {
                    if (Configure.TagRank2SubjectList.Contains(item.Text))
                        Configure.TagRank2SubjectList.Remove(item.Text);
                }
            }

            Configure.RankFilterTagName = cboRankRilter.Text;
            Configure.RankFilterTagList.Clear();
            foreach (var item in _TagConfigRecords)
            {
                if (item.Prefix != "")
                {
                    if (cboRankRilter.Text == "[" + item.Prefix + "]")
                        Configure.RankFilterTagList.Add(item.ID);
                }
                else
                {
                    if (cboRankRilter.Text == item.Name)
                        Configure.RankFilterTagList.Add(item.ID);
                }
            }
            Configure.ExportEpost = chkExportEPOST.Checked;
            Configure.Encode();
            Configure.Save();
        }

        private void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            #region 儲存檔案
            string inputReportName = "學年成績單合併欄位總表";
            string reportName = inputReportName;

            string path = Path.Combine(System.Windows.Forms.Application.StartupPath, "Reports");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, reportName + ".doc");

            if (File.Exists(path))
            {
                int i = 1;
                while (true)
                {
                    string newPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + (i++) + Path.GetExtension(path);
                    if (!File.Exists(newPath))
                    {
                        path = newPath;
                        break;
                    }
                }
            }

            try
            {
                //document.Save(path, Aspose.Words.SaveFormat.Doc);
                System.IO.FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                stream.Write(Properties.Resources.歡樂的合併欄位總表, 0, Properties.Resources.歡樂的合併欄位總表.Length);
                stream.Flush();
                stream.Close();
                System.Diagnostics.Process.Start(path);
            }
            catch
            {
                System.Windows.Forms.SaveFileDialog sd = new System.Windows.Forms.SaveFileDialog();
                sd.Title = "另存新檔";
                sd.FileName = reportName + ".doc";
                sd.Filter = "Word檔案 (*.doc)|*.doc|所有檔案 (*.*)|*.*";
                if (sd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {                        
                        System.IO.FileStream stream = new FileStream(sd.FileName, FileMode.Create, FileAccess.Write);
                        stream.Write(Properties.Resources.員林家商學年成績單樣版, 0, Properties.Resources.員林家商學年成績單樣版.Length);
                        stream.Flush();
                        stream.Close();

                    }
                    catch
                    {
                        FISCA.Presentation.Controls.MsgBox.Show("指定路徑無法存取。", "建立檔案失敗", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            #endregion
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void cblvSubject_CheckedChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lvSubject.Items)
                lvi.Checked = cblvSubject.Checked;
        }

        private void cblvSubjTag1_CheckedChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lvSubjTag1.Items)
                lvi.Checked = cblvSubject.Checked;
        }

        private void cblvSubjTag2_CheckedChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lvSubjTag2.Items)
                lvi.Checked = cblvSubject.Checked;
        }
    }
}
