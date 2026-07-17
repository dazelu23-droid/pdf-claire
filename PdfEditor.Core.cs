using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ClairePdfEditor
{
    public sealed class EditorProject
    {
        public string Title { get; set; }
        public int CurrentPage { get; set; }
        public List<EditorPage> Pages { get; set; }
        public List<EditorComment> Comments { get; set; }

        public EditorProject()
        {
            Title = "Untitled Document";
            CurrentPage = 0;
            Pages = new List<EditorPage>();
            Comments = new List<EditorComment>();
        }

        public static EditorProject CreateSample()
        {
            var project = new EditorProject { Title = "Project Proposal.pdf" };
            project.Pages.Add(new EditorPage
            {
                Number = 1,
                Rotation = 0,
                Header = "PROJECT PROPOSAL",
                Footer = "Confidential",
                Body = "PROJECT PROPOSAL\n\n1. Executive Summary\n\nOur team proposes a comprehensive solution designed to streamline operations, reduce costs, and drive sustainable growth. By leveraging proven methodologies and modern technology, we will deliver measurable results that align with your business objectives.\n\nThis initiative will enhance efficiency, improve customer satisfaction, and position your organization for long-term success.\n\n2. Objectives\n\n• Improve operational efficiency by 20%\n• Reduce costs through process optimization\n• Enhance data-driven decision making\n• Deliver a scalable and future-ready solution\n\n3. Timeline\n\nDiscovery     Requirements and analysis     2 weeks\nPlanning      Design and planning            3 weeks\nImplementation Development and configuration 6 weeks\nTesting       Testing and quality assurance  2 weeks"
            });
            project.Pages.Add(new EditorPage { Number = 2, Header = "PROJECT PROPOSAL", Footer = "Confidential", Body = "PROJECT PLAN\n\nMilestones, resources, and delivery details." });
            project.Pages.Add(new EditorPage { Number = 3, Header = "PROJECT PROPOSAL", Footer = "Confidential", Body = "FINANCIAL SUMMARY\n\nBudget assumptions and forecast." });
            project.Comments.Add(new EditorComment { PageNumber = 1, Author = "Jamie Lee", Text = "Consider adding specific metrics here." });
            return project;
        }

        public EditorPage ActivePage
        {
            get
            {
                if (Pages.Count == 0) Pages.Add(new EditorPage { Number = 1 });
                if (CurrentPage < 0) CurrentPage = 0;
                if (CurrentPage >= Pages.Count) CurrentPage = Pages.Count - 1;
                return Pages[CurrentPage];
            }
        }

        public void AddPage(string body)
        {
            Pages.Add(new EditorPage { Number = Pages.Count + 1, Body = body ?? String.Empty });
            CurrentPage = Pages.Count - 1;
        }

        public void RotateCurrent(int degrees)
        {
            int rotation = (ActivePage.Rotation + degrees) % 360;
            if (rotation < 0) rotation += 360;
            ActivePage.Rotation = rotation;
        }

        public IEnumerable<SearchHit> Search(string query)
        {
            var hits = new List<SearchHit>();
            if (String.IsNullOrWhiteSpace(query)) return hits;
            for (int i = 0; i < Pages.Count; i++)
            {
                string body = Pages[i].Body ?? String.Empty;
                int start = 0;
                while (start < body.Length)
                {
                    int index = body.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                    if (index < 0) break;
                    hits.Add(new SearchHit { PageNumber = i + 1, Index = index, Length = query.Length });
                    start = index + Math.Max(1, query.Length);
                }
            }
            return hits;
        }

        public void Save(string path)
        {
            string json = CreateSerializer().Serialize(this);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        public static EditorProject Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var project = CreateSerializer().Deserialize<EditorProject>(json);
            if (project == null) throw new InvalidDataException("The project file is empty or invalid.");
            if (project.Pages == null) project.Pages = new List<EditorPage>();
            if (project.Comments == null) project.Comments = new List<EditorComment>();
            return project;
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 256 };
        }
    }

    public sealed class EditorPage
    {
        public int Number { get; set; }
        public int Rotation { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public string Body { get; set; }
        public string RichTextData { get; set; }
        public string InkData { get; set; }
        public bool ShowPageNumber { get; set; }
        public string ImagePath { get; set; }
        public double ImageWidth { get; set; }
        public double ImageHeight { get; set; }
        public double CropLeft { get; set; }
        public double CropTop { get; set; }
        public double CropRight { get; set; }
        public double CropBottom { get; set; }
        public string Signature { get; set; }
        public string SourcePdfPath { get; set; }
        public int SourcePdfPageIndex { get; set; }

        public EditorPage()
        {
            Header = String.Empty;
            Footer = String.Empty;
            Body = String.Empty;
            RichTextData = String.Empty;
            InkData = String.Empty;
            ShowPageNumber = true;
            ImagePath = String.Empty;
            Signature = String.Empty;
            SourcePdfPath = String.Empty;
            SourcePdfPageIndex = -1;
            ImageWidth = 280;
            ImageHeight = 180;
        }
    }

    public sealed class EditorComment
    {
        public int PageNumber { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
    }

    public sealed class SearchHit
    {
        public int PageNumber { get; set; }
        public int Index { get; set; }
        public int Length { get; set; }
    }
}
