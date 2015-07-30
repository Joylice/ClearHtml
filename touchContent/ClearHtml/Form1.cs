using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections;
using System.Configuration;


namespace ClearHtml
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            textBox1.Enabled = true;
            textBox2.Enabled = true;
        }
        //根节点的替换
        //章节没有递进关系的处理
        private void selectFile_Click(object sender, EventArgs e)
        {
            textBox2.Enabled = false;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                string filename = folderBrowserDialog1.SelectedPath;
                textBox1.Text = filename;
                textBox1.ReadOnly = true;
                String[] files = Directory.GetFiles(filename, "*.html", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    XmlTextWriter xml = new XmlTextWriter("data.xml", Encoding.UTF8);
                    xml.Formatting = Formatting.Indented;
                    xml.WriteStartDocument();
                    xml.WriteStartElement("sections");
                    for (int i = 1; i <= files.Length; i++)
                    {
                        string fileContent = string.Empty;
                        using (var reader = new StreamReader(filename + @"\" + i + ".html"))
                        {
                            fileContent = reader.ReadToEnd();
                            string content = ClearHtml(fileContent);
                            xml.WriteStartElement("section" + i);
                            xml.WriteStartElement("Content");
                            xml.WriteCData(content.Trim());
                            xml.WriteEndElement();
                            xml.WriteEndElement();
                        }
                    }
                    xml.WriteEndElement();
                    xml.WriteEndDocument();
                    xml.Flush();
                    xml.Close();
                }
                else
                {
                    MessageBox.Show("该文件夹不存在HTML文件");
                }
            }

        }

        //替换HTML文件中的<p>标签和清除html文件中的html标签
        private string ClearHtml(string content)
        {
            string regexStr = @"<(?!/p).*?>";//去除所有标签，只剩/p
            content = Regex.Replace(content, regexStr, "", RegexOptions.IgnoreCase).Replace("\r", "").Replace("\n", "").Replace("\t", "");
            content = content.Replace(@"</p>", @"\n").Replace(@"&#160;", "").Replace(@"“", "。").Replace(@"#", "，");
            return content;
        }
        //获取head和metadata
        private void CreateNewXml(string path, string fileName)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<Resource>");
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlNode xn = doc.SelectSingleNode("record");
            XmlNode xFnode = doc.SelectSingleNode("record//adminMD");
            XmlNode xnode = doc.SelectSingleNode("record//ObjID");
            XmlNode catarows = doc.SelectSingleNode("record//Catalog");
            //换头
            XmlNode head = doc.SelectSingleNode("record//header");
            XmlNode metadata = doc.SelectSingleNode("record//metadata");
            XmlElement topxmle = doc.CreateElement("resource");
            topxmle.AppendChild(head);
            topxmle.AppendChild(metadata);
            sb.Append(topxmle.InnerXml);
            sb.AppendFormat("<File ObjID=\"{0}\">", xnode.InnerText);
            foreach (XmlElement e in catarows)
            {
                if (Convert.ToInt32(e.GetAttribute("ebookPageNum")) > 0)
                {
                    addChild(e, doc);
                }
            }
            sb.Append(catarows.InnerXml);
            sb.AppendFormat("</File>");
            sb.Append("</Resource>");
            using (StreamWriter sw = new StreamWriter(fileName, false, System.Text.Encoding.GetEncoding("UTF-8")))
            {
                sw.WriteLine(sb);
                sw.Flush();
                sw.Close();
            }
        }
        //递归添加content内容
        private void addChild(XmlElement one, XmlDocument doc)
        {
            XmlElement xmle;
            if (one.HasChildNodes)
            {
                xmle = AddContent(one, doc);
                one.PrependChild(xmle);
                for (int i = 1; i < one.ChildNodes.Count; i++)
                {
                    addChild((XmlElement)one.ChildNodes[i], doc);
                }
            }
            else
            {
                xmle = AddContent(one, doc);
                one.PrependChild(xmle);
            }

        }
        //识别上下文的内容端
        private XmlElement AddContent(XmlElement xmle, XmlDocument doc)
        {
            XmlElement one = doc.CreateElement("content");
            string contentStr = string.Empty;
            string chapterName = string.Empty;
            string ebookPageNum = string.Empty;
            string nextChapterName = string.Empty;
            string nexteBookPageNum = string.Empty;
            if (!xmle.HasChildNodes)
            {
                try
                {
                    //尾部处理
                    nextChapterName = xmle.NextSibling.Attributes["chapterName"].Value;
                    nexteBookPageNum = xmle.NextSibling.Attributes["ebookPageNum"].Value;
                }
                catch
                {
                    try
                    {
                        //并列章节且都有自己的子节点
                        nextChapterName = xmle.ParentNode.NextSibling.Attributes["chapterName"].Value;
                        nexteBookPageNum = xmle.ParentNode.NextSibling.Attributes["ebookPageNum"].Value;
                    }
                    catch
                    {
                        try
                        {
                            nextChapterName = xmle.ParentNode.ParentNode.NextSibling.Attributes["chapterName"].Value;
                            nexteBookPageNum = xmle.ParentNode.ParentNode.NextSibling.Attributes["ebookPageNum"].Value;
                        }
                        catch
                        {
                            nextChapterName = string.Empty;
                            nexteBookPageNum = string.Empty;
                        }
                    }
                }
                chapterName = xmle.Attributes["chapterName"].Value;
                ebookPageNum = xmle.Attributes["ebookPageNum"].Value;
                contentStr = GetContent(chapterName, ebookPageNum, nextChapterName, nexteBookPageNum);
            }
            else
            {
                chapterName = xmle.Attributes["chapterName"].Value;
                ebookPageNum = xmle.Attributes["ebookPageNum"].Value;
                nextChapterName = xmle.ChildNodes[0].Attributes["chapterName"].Value;
                nexteBookPageNum = xmle.ChildNodes[0].Attributes["ebookPageNum"].Value;
                contentStr = GetContent(chapterName, ebookPageNum, nextChapterName, nexteBookPageNum);

            }
            one.InnerText = contentStr;
            return one;
        }
        //获取要添加的节点内容
        private string GetContent(string curchapterName, string curEbookPageNum, string nextChapterName, string nextEbookPageNum)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load("data.xml");
            XmlNode one = doc.SelectSingleNode("sections");
            StringBuilder sb = new StringBuilder();
            int count = 0;
            if (string.IsNullOrEmpty(nextChapterName))
            {
                count = one.ChildNodes.Count;
            }
            else
            {
                count = Convert.ToInt32(nextEbookPageNum);
            }
            //包含一种情况：当前页与下一页相等
            for (int i = Convert.ToInt32(curEbookPageNum); i <= count; i++)
            {
                sb.Append(one.ChildNodes[i - 1].ChildNodes[0].InnerText);
            }
            int curAd = GetIndexOf(sb.ToString(), curchapterName);
            int nextAd = GetIndexOf(sb.ToString(), nextChapterName);
            if (string.IsNullOrEmpty(nextChapterName))//尾部处理
            {
                nextAd = -1;
            }
            string contentStr = string.Empty;
            //没有头，有尾
            if (curAd == -1 && nextAd != -1)
            {
                contentStr = sb.ToString().Substring(0, nextAd);
            }
            //有头没有尾
            if (curAd != -1 && nextAd == -1)
            {
                if (string.IsNullOrEmpty(nextChapterName))//尾部处理
                {
                    contentStr = sb.ToString().Substring(curAd);
                }
                else
                {
                    contentStr = sb.ToString().Substring(curAd).Replace(one.ChildNodes[Convert.ToInt32(nextEbookPageNum) - 1].ChildNodes[0].InnerText, "");
                }
            }
            //头尾都有
            if (curAd != -1 && nextAd != -1)
            {
                contentStr = sb.ToString().Substring(curAd, nextAd - curAd);
            }
            //头尾都没有
            if (curAd == -1 & nextAd == -1)
            {
                if (!string.IsNullOrEmpty(sb.ToString()))
                {
                    if (string.IsNullOrEmpty(nextChapterName))//尾部处理       
                    {
                        contentStr = sb.ToString();
                    }
                    else
                    {
                        contentStr = sb.ToString().Replace(one.ChildNodes[Convert.ToInt32(nextEbookPageNum) - 1].ChildNodes[0].InnerText, "");
                    }
                }
            }
            return contentStr;
        }
        //获取章节字符在内容的位置--匹配率超过50%且返回找到的第一个位置
        private int GetIndexOf(string contentStr, string chapterName)
        {    
            int countIndex = -1;
            //分隔符放入配置文件中
           // string delimiterStr = ConfigurationSettings.AppSettings["Delimiter"];
            XmlDocument doc = new XmlDocument();
            doc.Load("配置文件.xml");
            XmlNode one = doc.SelectSingleNode("record//add");
            string delimiterStr = one.Attributes["value"].Value;
            string[] charArr = delimiterStr.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
           // char[] charArr = new char[] { '"', ',', '—', '.', '（', '）', '《', '》', '”', '“', '。', '，', '(', ')','、' };       
            string[] chapterArr = chapterName.Split(charArr, StringSplitOptions.RemoveEmptyEntries);                 
            if (!string.IsNullOrEmpty(chapterName))
            {
                //判断所取内容是否含有章节名，若有则进行分隔若没有则直接返回-1
                if (IsExists(contentStr, chapterName, charArr))
                {
                    string subContent = contentStr;
                    subContent = ToDBC(subContent);
                 
                    if (chapterArr.Length > 1)
                    {
                        #region 分隔法
                        //for (int i = 0; i < contentArr.Length; i++)
                        //    {
                        //        double showCount = 0;
                        //        for (int j = 1; j < chapterList.Count; j++)
                        //        {
                        //            string contentPart = contentArr[i];
                        //            string chapterPart = chapterList[j].ToString();
                        //            clearContentStr(ref  contentPart, ref  chapterPart, charArr);
                        //            if (contentPart.Contains(chapterPart))
                        //            {
                        //                ++showCount;
                        //            }
                        //            if (showCount == chapterList.Count - 1)
                        //            {
                        //                int curChapterIndex = 0;
                        //                for (int k = 0; k < i; k++)
                        //                {
                        //                    curChapterIndex += contentArr[k].Length + chapterList[0].ToString().Length;
                        //                }
                        //                countIndex = curChapterIndex - chapterList[0].ToString().Length;
                        //            }
                        //        }
                        //    }
                        #endregion
                        #region 分段截取法
                        int lastIndex = 0;
                        FenGe(subContent, chapterName, chapterArr[0], charArr,ref lastIndex);
                        countIndex = lastIndex;
                        #endregion
                    }
                    else
                    {
                        countIndex = contentStr.IndexOf(chapterArr[0].ToString());
                    }
                }
                else
                {
                    countIndex = contentStr.IndexOf(chapterName);
                }
            }
          return countIndex;
        }
        //遍历章节名，逐次添加\n,分隔内容
        private void FenGe(string subContent, string chapterName, string chapterArrFirst, string[] charArr,ref int lastIndex)
        {
                string contentPartSub = subContent;
                int firstIndex = contentPartSub.IndexOf(chapterArrFirst);
                lastIndex += firstIndex + chapterArrFirst.Length;
                contentPartSub = contentPartSub.Substring(firstIndex);
                string contentPartClear = contentPartSub;
                clearContentStr(ref contentPartClear, ref chapterName, charArr);
                if (contentPartClear.IndexOf(chapterName) == 0)
                {
                    lastIndex = lastIndex - chapterArrFirst.Length;
                    return;
                }
                contentPartSub = contentPartSub.Substring(chapterArrFirst.Length);
             FenGe(contentPartSub, chapterName, chapterArrFirst, charArr,ref lastIndex);
        }
        //全文搜索是否存在章节名
        private bool IsExists(string contentStr, string chapterName, string[] charArr)
        {
            string tempContent = string.Empty;
            string tempChapterName = string.Empty;
            tempChapterName = chapterName;
            tempContent = contentStr;
            clearContentStr(ref tempContent, ref tempChapterName, charArr);
            if (tempContent.Contains(tempChapterName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //去掉所列的标点符号
        private void clearContentStr(ref  string contentStr,ref string chapterName,string[]charArr)
        {
            for (int i = 0; i < charArr.Length; i++)
            {
                chapterName = chapterName.Replace(charArr[i], "");
                contentStr = contentStr.Replace(charArr[i], "");
            }
            contentStr =ToDBC(contentStr).Replace(@"\n","").Replace(" ","").Trim();
            contentStr = contentStr.Replace("", "").Trim();
            chapterName = chapterName.Replace(" ", "").Trim();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "XML文件|*.xml";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox2.Text = openFileDialog1.FileName;

                textBox2.ReadOnly = true;
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            string filePath = textBox2.Text;
            string fileName = filePath.Substring(filePath.LastIndexOf(@"\") + 1);
            CreateNewXml(filePath, fileName);
        }
        //判断是否为数字的方法
        private bool IsNum(string num)
        {
            foreach (char c in num)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }
        //将数字的全角转成半角
        public String ToDBC(String input)
        {
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (char.IsDigit(c[i]))
                {
                    if (c[i] == 12288)
                    {
                        c[i] = (char)32;
                        continue;
                    }
                    if (c[i] > 65280 && c[i] < 65375)
                        c[i] = (char)(c[i] - 65248);
                }
            }
            return new String(c);
        }
        //将数字的半角变全角
        public static String ToSBC(String input)
        {
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 32)
                {
                    c[i] = (char)12288;
                    continue;
                }
                if (c[i] < 127)
                    c[i] = (char)(c[i] + 65248);
            }
            return new String(c);
        }
    }

}
