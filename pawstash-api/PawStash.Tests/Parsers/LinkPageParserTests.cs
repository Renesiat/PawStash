using PawStash.Common.Models;
using PawStash.Common.Parsers;
using PawStash.Common.Rules;

namespace PawStash.Tests.Parsers
{
    public class LinkPageParserTests
    {
        private static readonly Uri PageUri = new("https://www.example.org/recipes/borsch");

        [Fact]
        public void Parse_OpenGraphTags_TakesTitleDescriptionAndPicture()
        {
            LinkPageInfo page = Parse("""
                <head>
                <title>Сайт</title>
                <meta property="og:title" content="Борщ">
                <meta property="og:description" content="Рецепт борщу">
                <meta property="og:image" content="https://cdn.example.org/borsch.jpg">
                </head>
                """);

            Assert.Equal("Борщ", page.Title);
            Assert.Equal("Рецепт борщу", page.Description);
            Assert.Equal(new[] { new Uri("https://cdn.example.org/borsch.jpg") }, page.PictureUris);
        }

        [Fact]
        public void Parse_WithoutOpenGraph_UsesTwitterTags()
        {
            LinkPageInfo page = Parse("""
                <head>
                <title>Сайт</title>
                <meta name="twitter:title" content="Борщ">
                <meta name="twitter:description" content="Рецепт">
                <meta name="twitter:image" content="https://cdn.example.org/tw.jpg">
                </head>
                """);

            Assert.Equal("Борщ", page.Title);
            Assert.Equal("Рецепт", page.Description);
            Assert.Equal(new Uri("https://cdn.example.org/tw.jpg"), Assert.Single(page.PictureUris));
        }

        [Fact]
        public void Parse_OnlyTitleAndDescription_UsesThem()
        {
            LinkPageInfo page = Parse("""<head><title>Борщ — Вікіпедія</title><meta name="description" content="Страва"></head>""");

            Assert.Equal("Борщ — Вікіпедія", page.Title);
            Assert.Equal("Страва", page.Description);
            Assert.Empty(page.PictureUris);
        }

        [Fact]
        public void Parse_EmptyPage_FindsNothing()
        {
            LinkPageInfo page = Parse("<html><body>Нічого</body></html>");

            Assert.Null(page.Title);
            Assert.Null(page.Description);
            Assert.Empty(page.PictureUris);
        }

        [Fact]
        public void Parse_HtmlEntities_AreDecoded()
        {
            LinkPageInfo page = Parse("""<head><meta property="og:title" content="Борщ &amp; пампушки &quot;як у бабусі&quot; &#128218;"></head>""");

            Assert.Equal("Борщ & пампушки \"як у бабусі\" 📚", page.Title);
        }

        [Fact]
        public void Parse_SpacesAndLineBreaks_AreCollapsed()
        {
            LinkPageInfo page = Parse("<head><title>\n   Борщ\n\n   з   пампушками  \n</title></head>");

            Assert.Equal("Борщ з пампушками", page.Title);
        }

        [Fact]
        public void Parse_LongTitleAndDescription_AreCut()
        {
            string html = $"""
                <head>
                <meta property="og:title" content="{new string('а', 400)}">
                <meta property="og:description" content="{new string('б', 3000)}">
                </head>
                """;

            LinkPageInfo page = Parse(html);

            Assert.Equal(FileSystemItemRules.MaxNameLength, page.Title!.Length);
            Assert.Equal(FileSystemItemRules.MaxDescriptionLength, page.Description!.Length);
        }

        [Fact]
        public void Parse_AttributeOrderAndQuotes_DoNotMatter()
        {
            LinkPageInfo page = Parse("""
                <head>
                <META CONTENT='Борщ' PROPERTY='og:title'>
                <meta content=Рецепт name=description>
                </head>
                """);

            Assert.Equal("Борщ", page.Title);
            Assert.Equal("Рецепт", page.Description);
        }

        [Fact]
        public void Parse_TagsAfterHead_AreIgnored()
        {
            LinkPageInfo page = Parse("""
                <head><title>Сайт</title></head>
                <body><meta property="og:title" content="Не те"><meta property="og:image" content="/body.jpg"></body>
                """);

            Assert.Equal("Сайт", page.Title);
            Assert.Empty(page.PictureUris);
        }

        [Fact]
        public void Parse_FirstOccurrenceOfTag_Wins()
        {
            LinkPageInfo page = Parse("""<head><meta property="og:title" content="Перша"><meta property="og:title" content="Друга"></head>""");

            Assert.Equal("Перша", page.Title);
        }

        [Fact]
        public void Parse_RelativeAddresses_ResolveAgainstPage()
        {
            LinkPageInfo page = Parse("""
                <head>
                <meta property="og:image" content="/img/root.jpg">
                <meta name="twitter:image" content="local.jpg">
                <link rel="apple-touch-icon" href="//static.example.org/apple.png">
                </head>
                """);

            Assert.Equal(
                new[]
                {
                    new Uri("https://www.example.org/img/root.jpg"),
                    new Uri("https://www.example.org/recipes/local.jpg"),
                    new Uri("https://static.example.org/apple.png")
                },
                page.PictureUris);
        }

        [Fact]
        public void Parse_BaseAddress_IsUsedForRelativeAddresses()
        {
            LinkPageInfo page = Parse("""<head><base href="https://cdn.example.org/root/"><meta property="og:image" content="pic.jpg"></head>""");

            Assert.Equal(new Uri("https://cdn.example.org/root/pic.jpg"), Assert.Single(page.PictureUris));
        }

        [Fact]
        public void Parse_Icons_AppleTouchIconComesBeforePngIconAndIcoIsSkipped()
        {
            LinkPageInfo page = Parse("""
                <head>
                <link rel="icon" href="/favicon.ico">
                <link rel="icon" type="image/png" href="/icon-192">
                <link rel="shortcut icon" href="/icon-32.png?v=2">
                <link rel="apple-touch-icon-precomposed" href="/apple.png">
                <link rel="stylesheet" href="/style.css">
                </head>
                """);

            Assert.Equal(
                new[]
                {
                    new Uri("https://www.example.org/apple.png"),
                    new Uri("https://www.example.org/icon-192"),
                    new Uri("https://www.example.org/icon-32.png?v=2")
                },
                page.PictureUris);
        }

        [Fact]
        public void Parse_PreviewPictureComesBeforeIcons()
        {
            LinkPageInfo page = Parse("""
                <head>
                <link rel="apple-touch-icon" href="/apple.png">
                <meta property="og:image" content="https://cdn.example.org/preview.jpg">
                </head>
                """);

            Assert.Equal(new Uri("https://cdn.example.org/preview.jpg"), page.PictureUris[0]);
        }

        [Fact]
        public void Parse_NonWebPictureAddresses_AreSkipped()
        {
            LinkPageInfo page = Parse("""
                <head>
                <meta property="og:image" content="data:image/png;base64,AAAA">
                <meta name="twitter:image" content="javascript:alert(1)">
                <link rel="apple-touch-icon" href="ftp://example.org/apple.png">
                </head>
                """);

            Assert.Empty(page.PictureUris);
        }

        [Fact]
        public void Parse_SamePictureTwice_IsListedOnce()
        {
            LinkPageInfo page = Parse("""
                <head>
                <meta property="og:image" content="https://cdn.example.org/a.jpg">
                <meta name="twitter:image" content="https://cdn.example.org/a.jpg">
                </head>
                """);

            Assert.Single(page.PictureUris);
        }

        private static LinkPageInfo Parse(string html)
        {
            return LinkPageParser.Parse(html, PageUri);
        }
    }
}
