using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ZhihuDatabase
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string ImageServer = "https://pi.hehehey.com/file/img_zhihu/";
        private const int PageSize = 1;

        private const string HtmlTemplate = "<html>\n" +
                                            "<head>\n" +
                                            "    <meta http-equiv=\"X-UA-Compatible\" content=\"IE=10\">\n" +
                                            "    <meta charset='UTF-8'>\n" +
                                            "    <!-- MDL -->\n" +
                                            "    <link href=\"https://www.hehehey.com/lib/material.min.css\" rel=\"stylesheet\">\n" +
                                            "    <link href=\"https://www.hehehey.com/lib/material-icons.css\" rel=\"stylesheet\">\n" +
                                            "\n" +
                                            "    <!-- Code highlighting -->\n" +
                                            "    <link href=\"https://www.hehehey.com/lib/atom-one-dark.css\" rel=\"stylesheet\">\n" +
                                            "\n" +
                                            "    <link href=\"https://www.hehehey.com/css/common.css\" rel=\"stylesheet\">\n" +
                                            "    <link href=\"https://www.hehehey.com/css/page-detail.css\" rel=\"stylesheet\">\n" +
                                            "    <style>\n" +
                                            "        body {{\n" +
                                            "            font-family: Consolas, Microsoft YaHei, monospace;\n" +
                                            "        }}\n" +
                                            "    </style>\n" +
                                            "</head>\n" +
                                            "<body>\n" +
                                            "<div class=\"mdl-layout mdl-js-layout mdl-layout--fixed-header\">\n" +
                                            "    <main class=\"mdl-layout__content\">\n" +
                                            "{0}" +
                                            "    </main>\n" +
                                            "</div>\n" +
                                            "<script src=\"https://www.hehehey.com/lib/material.min.js\"></script>\n" +
                                            "<script src=\"https://www.hehehey.com/lib/highlight.pack.js\"></script>\n" +
                                            "<script src=\"https://www.hehehey.com/js/util.js\"></script>\n" +
                                            "<script>\n" +
                                            "    //noinspection JSUnresolvedVariable\n" +
                                            "    hljs.initHighlightingOnLoad();\n" +
                                            "</script>\n" +
                                            "</body>\n" +
                                            "</html>";

        private const string HtmlContentTemplate =
            "        <h1 class='text-monospace mdl-typography--title'>Result {2,3:D3}:</h1>\n" +
            "        <p>{3}</p>\n" +
            "        <h2 class='text-monospace mdl-typography--subhead'>Json Representation</h2>\n" +
            "        <pre><code class=\"json\">{0}</code></pre>\n" +
            "        <h2 class='text-monospace mdl-typography--subhead'>Html Content</h2>\n" +
            "        <div>{1}</div>\n";

        private const string HtmlIndex = "        <h1 class='text-monospace mdl-typography--title'>Qucik Start</h1>\n" +
                                         "        <p><ol><li>Connect to mongodb.</li><li>Enter search query.</li><li>Search.</li></ol></p>\n"
            ;

        private MongoClient _client;
        private DatabaseQuery _query;

        public MainWindow()
        {
            InitializeComponent();
            WebContent.NavigateToString(string.Format(HtmlTemplate, HtmlIndex));
        }

        private void TextBoxEnterKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (((TextBox) sender).Name == TextBoxFilter.Name)
            {
                Search(ButtonSearch, null);
            }
            else if (((TextBox) sender).Name == TextBoxConnect.Name)
            {
                OnConnect(null, null);
            }
            else if (((TextBox)sender).Name == TextPage.Name)
            {
                Search(null, null);
            }
        }

        private async void OnConnect(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                TextBoxConnect.IsEnabled = false;
                ButtonConnect.IsEnabled = false;
                ButtonConnect.Content = "connecting...";

                var connStr = TextBoxConnect.Text;

                var result = await Task.Run(() =>
                {
                    MongoClient client = null;
                    var names = new ArrayList();
                    try
                    {
                        client = new MongoClient(connStr);
                        var collections = client.GetDatabase("zhihu").ListCollections().ToList();
                        foreach (var collection in collections)
                        {
                            names.Add(collection["name"]);
                        }
                        return new {Names = names, Client = client, Error = false, Exception = new Exception()};
                    }
                    catch (Exception exception)
                    {
                        return new {Names = names, Client = client, Error = true, Exception = exception};
                    }
                });

                if (result.Error)
                {
                    _client = null;
                    TextBlockInfo.Text = "connection error: " + result.Exception.GetType().FullName;
                    WebContent.NavigateToString(string.Format(HtmlTemplate,
                        "<h1 class='text-monospace mdl-typography--title'>Error</h1><p>Failed to connect: <pre><code>" +
                        result.Exception + "</code></pre></p>\n"));
                    TextBoxConnect.IsEnabled = true;
                    ButtonConnect.IsEnabled = true;
                    ButtonConnect.Content = "connect";
                }
                else
                {
                    _client = result.Client;
                    TextBlockInfo.Text = "collections: " + string.Join(", ", result.Names.ToArray());

                    ButtonConnect.IsEnabled = true;
                    ButtonConnect.Content = "disconnect";
                    WebContent.NavigateToString(string.Format(HtmlTemplate, HtmlIndex));
                }
            }
            else
            {
                _client = null;
                TextBoxConnect.IsEnabled = true;
                TextBlockInfo.Text = "";
                ButtonConnect.Content = "connect";
                PanelPage.Visibility = Visibility.Hidden;
                WebContent.NavigateToString(string.Format(HtmlTemplate, HtmlIndex));
            }
        }

        [SuppressMessage("ReSharper", "SpecifyACultureInStringConversionExplicitly")]
        private async void Search(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                WebContent.NavigateToString(string.Format(HtmlTemplate,
                    "<h1 class='text-monospace mdl-typography--title'>Error</h1><p>You must be connected to a mongodb first!</p>\n"));
                return;
            }
            ButtonPreviousPage.IsEnabled = false;
            ButtonNextPage.IsEnabled = false;
            TextPage.IsEnabled = false;

            WebContent.NavigateToString(string.Format(HtmlTemplate,
                "<h1 class='text-monospace mdl-typography--title'>Please Wait</h1><p>Searching...</p>\n"));

            var toPageNumber = Convert.ToInt32(TextPage.Text);
            if (sender != null && ((Button) sender).Name == ButtonPreviousPage.Name)
            {
                toPageNumber--;
            }
            else if (sender != null && ((Button) sender).Name == ButtonNextPage.Name)
            {
                toPageNumber++;
            }
            else if (sender != null && ((Button)sender).Name == ButtonSearch.Name)
            {
                toPageNumber = 1;
                _query = new DatabaseQuery(_client, "zhihu", TextBoxFilter.Text, PageSize);
                await _query.Init();
            }

            try
            {
                var contentBuilder = new StringBuilder();
                var count = _query.PageSize * (toPageNumber - 1);
                var data = await _query.GetPage(toPageNumber);
                foreach (var one in data)
                {
                    if (_query.CollectionName.Equals("answer"))
                    {
                        var content = one.GetValue("content").ToString();
                        var answerUrl =
                            $"https://www.zhihu.com/question/{one.GetValue("questionId")}/answer/{one.GetValue("answerId")}";
                        var authorUrl = $"https://www.zhihu.com/people/{one.GetValue("authorId")}";
                        one.Set("content", "...");
                        contentBuilder.Append(string.Format(HtmlContentTemplate,
                            JsonHelper.PrettyPrint(one.ToString()),
                            StripHtml(content),
                            count++,
                            $"<a target='_blank' href='{answerUrl}'>{answerUrl}</a></p><p><a target='_blank' href='{authorUrl}'>{authorUrl}</a>"));
                    }
                    else if (_query.CollectionName.Equals("member"))
                    {
                        var url = $"https://www.zhihu.com/people/{one.GetValue("memberId")}";
                        contentBuilder.Append(String.Format(HtmlContentTemplate,
                            JsonHelper.PrettyPrint(one.ToString()),
                            $"<p>Avatar: <img src='{one.GetValue("avatarUrl").ToString().Replace("https://", ImageServer)}'></p>",
                            count++,
                            $"<a target='_blank' href='{url}'>{url}</a>"));
                    }
                    else
                        contentBuilder.Append(string.Format(HtmlContentTemplate, JsonHelper.PrettyPrint(one.ToString()),
                            "", count++, ""));
                }

                WebContent.NavigateToString(string.Format(HtmlTemplate, contentBuilder));
                PanelPage.Visibility = Visibility.Visible;
            }
            catch (Exception exception)
            {
                WebContent.NavigateToString(string.Format(HtmlTemplate,
                    "<h1 class='text-monospace mdl-typography--title'>Error</h1><p>Failed to search: <pre><code>" +
                    exception + "</code></pre></p>\n"));
                PanelPage.Visibility = Visibility.Hidden;
            }

            TextBoxFilter.Text = _query.SearchString;
            ButtonPreviousPage.IsEnabled = true;
            ButtonNextPage.IsEnabled = true;
            TextPage.IsEnabled = true;
            TextPageCount.Text = $" / {_query.PageCount,3:D3}";
            TextPage.Text = $"{_query.CurrentPageNumber, 3:D3}";
            if (_query.CurrentPageNumber == 1)
            {
                ButtonPreviousPage.IsEnabled = false;
            }
            if (_query.CurrentPageNumber == _query.PageCount)
            {
                ButtonNextPage.IsEnabled = false;
            }
        }

        static string StripHtml(string input)
        {
            string pattern =
                @"<noscript>[^<]*<img[^<]*<\/noscript><img[^>]+data\-actualsrc=\""https?:\/\/([^\""]+)\""[^>]*>";
            return Regex.Replace(input, pattern, @"<p><img src='" + ImageServer + "$1'></p>",
                RegexOptions.Compiled | RegexOptions.Multiline);
        }
    }

    static class JsonHelper
    {
        private const string IndentString = "    ";

        public static string PrettyPrint(string json)
        {
            json = json.Replace(", ", ",").Replace("{ ", "{").Replace("<", "&lt").Replace(">", "&gt");
            var indentation = 0;
            var quoteCount = 0;
            var result =
                from ch in json
                let quotes = ch == '"' ? quoteCount++ : quoteCount
                let lineBreak = ch == ',' && quotes % 2 == 0
                    ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(IndentString, indentation))
                    : null
                let openChar = ch == '{' || ch == '['
                    ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(IndentString, ++indentation))
                    : ch.ToString()
                let closeChar = ch == '}' || ch == ']'
                    ? Environment.NewLine + string.Concat(Enumerable.Repeat(IndentString, --indentation)) + ch
                    : ch.ToString()
                select lineBreak ?? (openChar.Length > 1
                           ? openChar
                           : closeChar);

            return string.Concat(result);
        }
    }
}