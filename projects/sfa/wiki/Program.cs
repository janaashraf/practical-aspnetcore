using FluentValidation;
using FluentValidation.AspNetCore;
using Ganss.Xss;
using HtmlBuilders;
using LiteDB;
using Markdig;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Scriban;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static HtmlBuilders.HtmlTags;
const string DisplayDateFormat = "MMMM dd, yyyy";
const string HomePageName = "home-page";
const string HtmlMime = "text/html";

var builder = WebApplication.CreateBuilder();
builder.Services
  .AddSingleton<Wiki>()
  .AddSingleton<Render>()
  .AddAntiforgery()
  .AddMemoryCache()
  .AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins("http://your-wiki.somee.com")
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

// Load home page
app.MapGet("/", (Wiki wiki, Render render) =>
{
    try
    {
        Page? page = wiki.GetPage(HomePageName);

        if (page is not object)
            return Results.Redirect($"/{HomePageName}");

        return Results.Text(render.BuildPage(HomePageName, atBody: () =>
            new[]
            {
          RenderPageContent(page),
          RenderPageAttachments(page),
          A.Href($"/edit?pageName={HomePageName}").Class("uk-button uk-button-default uk-button-small").Append("Edit").ToHtmlString()
            },
            atSidePanel: () => AllPages(wiki)
          ).ToString(), HtmlMime);
    }
    catch (Exception ex) {
       return HandleError(ex, app.Logger, "Error while handling request for '/'");
    }
});

//Load the new page
app.MapGet("/new-page", (string? pageName) =>
{
    try
    {
        if (string.IsNullOrEmpty(pageName))
            Results.Redirect("/");

        var page = ToKebabCase(pageName!);
        return Results.Redirect($"/{page}");
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for '/new-page'");
    }
});

// Edit a wiki page
app.MapGet("/edit", (string pageName, HttpContext context, Wiki wiki, Render render, IAntiforgery antiForgery) =>
{
    try
    {
        Page? page = wiki.GetPage(pageName);
        if (page is not object)
            return Results.NotFound();

        return Results.Text(render.BuildEditorPage(pageName,
          atBody: () =>

            new[]
            {
          BuildForm(new PageInput(page!.Id, pageName, page.Content, null), path: $"{pageName}", antiForgery: antiForgery.GetAndStoreTokens(context)),
          RenderPageAttachmentsForEdit(page!, antiForgery.GetAndStoreTokens(context)),
          RenderDeletePageButton(page!, antiForgery: antiForgery.GetAndStoreTokens(context))

            },
          atSidePanel: () =>
          {
              var list = new List<string>();
              // Do not show delete button on home page
              if (!pageName!.ToString().Equals(HomePageName, StringComparison.Ordinal))
              list.Add(Br.ToHtmlString());
              list.AddRange(AllPagesForEditing(wiki));
              return list;
          }).ToString(), HtmlMime);
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for '/edit");
    }
});

// Deal with attachment download
app.MapGet("/attachment", (string fileId, Wiki wiki) =>
{
    try
    {
        var file = wiki.GetFile(fileId);
        if (file is not object)
            return Results.NotFound();

        app!.Logger.LogInformation("Attachment " + file.Value.meta.Id + " - " + file.Value.meta.Filename);

        return Results.File(file.Value.file, file.Value.meta.MimeType);
    }
    catch (Exception ex)
    {
        return HandleError(ex, app.Logger, "Error while handling request for '/attachment");
    }
});

// Load a wiki page
app.MapGet("/{pageName}", (string pageName, HttpContext context, Wiki wiki, Render render, IAntiforgery antiForgery) =>
{ try
    {
        pageName = pageName ?? "";

        Page? page = wiki.GetPage(pageName);

        if (page is object)
        {
            return Results.Text(render.BuildPage(pageName, atBody: () =>
              new[]
              {
            RenderPageContent(page),
            RenderPageAttachments(page),
            Div.Class("last-modified").Append("Last modified: " + page!.LastModifiedUtc.ToString(DisplayDateFormat)).ToHtmlString(),
            A.Href($"/edit?pageName={pageName}").Append("Edit").ToHtmlString()
              },
              atSidePanel: () => AllPages(wiki)
            ).ToString(), HtmlMime);
        }
        else
        {
            return Results.Text(render.BuildEditorPage(pageName,
            atBody: () =>
              new[]
              {
            BuildForm(new PageInput(null, pageName, string.Empty, null), path: pageName, antiForgery: antiForgery.GetAndStoreTokens(context))
              },
            atSidePanel: () => AllPagesForEditing(wiki)).ToString(), HtmlMime);
        }
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for 'Get/{pageName}");
    }
});

// Delete a page
app.MapPost("/delete-page", async (HttpContext context, IAntiforgery antiForgery, Wiki wiki) =>
{  try
    {
        await antiForgery.ValidateRequestAsync(context);
        var id = context.Request.Form["Id"];

        if (StringValues.IsNullOrEmpty(id))
        {
            app.Logger.LogWarning($"Unable to delete page because form Id is missing");
            return Results.Redirect("/");
        }

        var (isOk, exception) = wiki.DeletePage(Convert.ToInt32(id), HomePageName);

        if (!isOk && exception is object)
            app.Logger.LogError(exception, $"Error in deleting page id {id}");
        else if (!isOk)
            app.Logger.LogError($"Unable to delete page id {id}");

        return Results.Redirect("/");
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for '/delete-page");
    }
});

//Delete an attachment
app.MapPost("/delete-attachment", async (HttpContext context, IAntiforgery antiForgery, Wiki wiki)=>
{
    try
    {
        await antiForgery.ValidateRequestAsync(context);
        var id = context.Request.Form["Id"];

        if (StringValues.IsNullOrEmpty(id))
        {
            app.Logger.LogWarning($"Unable to delete attachment because form Id is missing");
            return Results.Redirect("/");
        }

        var pageId = context.Request.Form["PageId"];
        if (StringValues.IsNullOrEmpty(pageId))
        {
            app.Logger.LogWarning($"Unable to delete attachment because form PageId is missing");
            return Results.Redirect("/");
        }

        var (isOk, page, exception) = wiki.DeleteAttachment(Convert.ToInt32(pageId), id.ToString());

        if (!isOk)
        {
            if (exception is object)
                app.Logger.LogError(exception, $"Error in deleting page attachment id {id}");
            else
                app.Logger.LogError($"Unable to delete page attachment id {id}");

            if (page is object)
                return Results.Redirect($"/{page.Name}");
            else
                return Results.Redirect("/");
        }

        return Results.Redirect($"/{page!.Name}");
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for '/delete-attachment");
    }
});

// Add or update a wiki page
app.MapPost("/{pageName}", async (HttpContext context, Wiki wiki, Render render, IAntiforgery antiForgery)  =>
{  try
    {
        var pageName = context.Request.RouteValues["pageName"] as string ?? "";
        await antiForgery.ValidateRequestAsync(context);

        PageInput input = PageInput.From(context.Request.Form);

        var modelState = new ModelStateDictionary();
        var validator = new PageInputValidator(pageName, HomePageName);
        validator.Validate(input).AddToModelState(modelState, null);

        if (!modelState.IsValid)
        {
            return Results.Text(render.BuildEditorPage(pageName,
              atBody: () =>
                new[]
                {
              BuildForm(input, path: $"{pageName}", antiForgery: antiForgery.GetAndStoreTokens(context), modelState)
                },
              atSidePanel: () => AllPages(wiki)).ToString(), HtmlMime);
        }

        var (isOk, p, ex) = wiki.SavePage(input);
        if (!isOk)
        {
            app.Logger.LogError(ex, "Problem in saving page");
            return Results.Problem("Problem in saving page");
        }

        return Results.Redirect($"/{p!.Name}");
    }
    catch (Exception ex) {
        return HandleError(ex, app.Logger, "Error while handling request for 'Post/{pageName}");
    }
});

//Load tags for the content
app.MapPost("/get-tags", async (HttpContext context) =>
{
    var apiKey = "ftdVb7D43Eiw"; // Replace with your actual API key
    var endpoint = "https://api.uclassify.com/v1/uclassify/topics/classify";
    var client = new HttpClient();

    client.DefaultRequestHeaders.Add("Authorization", $"Token {apiKey}");

    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

    var response = await client.PostAsync(endpoint, content);
    var responseJson = await response.Content.ReadAsStringAsync();

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(responseJson);
});

//Check if the page name already exists
app.MapGet("/check-page-exists", (string pageName, Wiki wiki) =>
{
    var pageNameKebab = ToKebabCase(pageName!);
    Page? page = wiki.GetPage(pageNameKebab);
    return Results.Json(new { exists = page != null });
});
await app.RunAsync();

// End of the web part

// Copied from https://www.30secondsofcode.org/c-sharp/s/to-kebab-case
static string ToKebabCase(string str)
{
    Regex pattern = new Regex(@"[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+");
    return string.Join("-", pattern.Matches(str)).ToLower();
}

//Common method that handle exceptions
static IResult HandleError(Exception ex, ILogger logger, string logMessage)
{
    logger.LogError(ex, logMessage);
    return Results.Text("An error occurred while processing your request.", HtmlMime);
}

//Returns the HTML of the list of wiki pages 
static string[] AllPages(Wiki wiki) => new[]
{
  @"<span class=""uk-label"">Pages</span>",
  @"<ul class=""uk-list"">",
  string.Join("",
    wiki.ListAllPages().OrderBy(x => x.Name)
      .Select(x => Li.Append(A.Href(x.Name).Append(x.Name)).ToHtmlString()
    )
  ),
  "</ul>"
};

//Return the HTML of the list of pages in the editing mode
static string[] AllPagesForEditing(Wiki wiki)
{
    static string KebabToNormalCase(string txt) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(txt.Replace('-', ' '));

    return new[]
    {
      @"<span class=""uk-label"">Pages</span>",
      @"<ul class=""uk-list"">",
      string.Join("",
        wiki.ListAllPages().OrderBy(x => x.Name)
          .Select(x => Li.Append(Div.Class("uk-inline")
              .Append(Span.Class("uk-form-icon").Attribute("uk-icon", "icon: copy"))
              .Append(Input.Text.Value($"[{KebabToNormalCase(x.Name)}](/{x.Name})").Class("uk-input uk-form-small").Style("cursor", "pointer").Attribute("onclick", "copyMarkdownLink(this);"))
          ).ToHtmlString()
        )
      ),
      "</ul>"
    };
}

//Converts Markdown content into safe HTML
static string RenderMarkdown(string str)
{
    var sanitizer = new HtmlSanitizer();
    return sanitizer.Sanitize(Markdown.ToHtml(str, new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().UseAdvancedExtensions().Build()));
}

//Returns HTML of the content
static string RenderPageContent(Page page)
{
    var contentHtml = "";
    contentHtml += RenderMarkdown(page.Content);
    if (!page.Name.Equals(HomePageName, StringComparison.OrdinalIgnoreCase))
    {
        var cleanedPageName = page.Name?.Replace("-", " ");
        var wikipediaButton = Div.Append(A.Href($"https://en.wikipedia.org/wiki/{cleanedPageName}")
                               .Append("Get help from Wikipedia")
                               .Class("custom uk-button uk-button-default uk-width-auto@s uk-margin-top uk-margin-bottom custom-button-height"));


        contentHtml += "<hr>" + wikipediaButton.ToHtmlString();
    }

    return contentHtml;
}

//Returns HTML of delete page button
static string RenderDeletePageButton(Page page, AntiforgeryTokenSet antiForgery)
{   if (page.Name != HomePageName)
    {
        var antiForgeryField = Input.Hidden.Name(antiForgery.FormFieldName).Value(antiForgery.RequestToken!);
        HtmlTag id = Input.Hidden.Name("Id").Value(page.Id.ToString());
        var submit = Div.Style("margin-top", "20px").Style("margin-bottom", "20px").Append(Button.Class("uk-button uk-button-danger delete-btn").Append("Delete Page"));

        var form = Form
                   .Attribute("method", "post")
                   .Attribute("action", $"/delete-page")
                   .Attribute("onsubmit", $"return confirm('Please confirm to delete this page');")
                     .Append(antiForgeryField)
                     .Append(id)
                     .Append(submit);

        return form.ToHtmlString();
    }
    return "";
}

//Returns HTML of attachments in the editing mode
static string RenderPageAttachmentsForEdit(Page page, AntiforgeryTokenSet antiForgery)
{
    if (page.Attachments.Count == 0)
        return string.Empty;

    var label = Span.Class("uk-background-muted").Append("Attachments");
    var list = Ul.Class("uk-list");

    HtmlTag CreateEditorHelper(Attachment attachment) =>
      Span.Class("uk-inline")
          .Append(Span.Class("uk-form-icon").Attribute("uk-icon", "icon: copy"))
          .Append(Input.Text.Value($"[{attachment.FileName}](/attachment?fileId={attachment.FileId})")
            .Class("uk-input uk-form-small uk-form-width-small")
            .Style("cursor", "pointer")
            .Attribute("onclick", "copyMarkdownLink(this);")
          );

    static HtmlTag CreateDelete(int pageId, string attachmentId, AntiforgeryTokenSet antiForgery)
    {
        var antiForgeryField = Input.Hidden.Name(antiForgery.FormFieldName).Value(antiForgery.RequestToken!);
        var id = Input.Hidden.Name("Id").Value(attachmentId.ToString());
        var name = Input.Hidden.Name("PageId").Value(pageId.ToString());

        var submit = Button.Class("uk-button uk-button-danger uk-button-small").Append(Span.Attribute("uk-icon", "icon: close; ratio: .50;"));
        var form = Form
               .Style("display", "inline")
               .Attribute("method", "post")
               .Attribute("action", $"/delete-attachment")
               .Attribute("onsubmit", $"return confirm('Please confirm to delete this attachment');")
                 .Append(antiForgeryField)
                 .Append(id)
                 .Append(name)
                 .Append(submit);

        return form;
    }

    foreach (var attachment in page.Attachments)
    {
        list = list.Append(Li
          .Append(CreateEditorHelper(attachment))
          .Append(CreateDelete(page.Id, attachment.FileId, antiForgery))
        );
    }
    return label.ToHtmlString() + list.ToHtmlString();
}

//Returns HTML of attachments
static string RenderPageAttachments(Page page)
{
    if (page.Attachments.Count == 0)
        return string.Empty;

    var label = Span.Class("uk-label").Append("Attachments");
    var list = Ul.Class("uk-list uk-list-disc");
    list = page.Attachments.Aggregate(list, (currentList, attachment) =>
    {
        if (!attachment.MimeType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
        {
            currentList = currentList.Append(
                Li.Append(
                    A.Href($"/attachment?fileId={attachment.FileId}")
                     .Append(attachment.FileName)
                )
            );
        }
        else
        {
            var modalId = attachment.FileName.Split(".")[0];

            currentList = currentList.Append(
                Li.Append(
                    A.Href($"#{modalId}")
                     .Attribute("uk-toggle", "")
                     .Append(attachment.FileName)
                )
            );

            currentList = currentList.Append(
                Div.Id(modalId)
                   .Attribute("uk-modal", "")
                   .Append(
                       Div.Class("uk-modal-dialog uk-modal-body")
                          .Append(
                              H2.Class("uk-modal-title")
                                .Append(attachment.FileName)
                          )
                          .Append(
                              Img.Class("uk-width-1-1")
                                 .Attribute("src", $"/attachment?fileId={attachment.FileId}")
                                 .Attribute("loading", "lazy")
                          )

                   )
            );
        }

        return currentList;
    });
    return label.ToHtmlString() + list.ToHtmlString();
}

// Build the wiki input form 
static string BuildForm(PageInput input, string path, AntiforgeryTokenSet antiForgery, ModelStateDictionary? modelState = null)
{
    bool IsFieldOK(string key) => modelState!.ContainsKey(key) && modelState[key]!.ValidationState == ModelValidationState.Invalid;

    var antiForgeryField = Input.Hidden.Name(antiForgery.FormFieldName).Value(antiForgery.RequestToken!);

    var nameField = Div
      .Append(Label.Class("uk-form-label").Append(nameof(input.Name)))
      .Append(Div.Class("uk-form-controls")
        .Append(Input.Text.Class("uk-input").Name("Name").Value(input.Name))
    .Style("margin-bottom", "20px")
      );
    
     var tagsButton = Button.Class("uk-button uk-button-muted tags")
                                 .Attribute("id", "tags")
                                 .Attribute("type", "button")
                                 .Style("margin-bottom", "20px")
                                 .Append("Show Tags");
    var tagsDiv = Div.Id("classification")
                     .Style("margin-bottom", "20px");
    var contentField = Div
      .Append(Label.Class("uk-form-label").Append(nameof(input.Content)))
      .Append(Div.Class("uk-form-controls")
        .Append(Textarea.Name("Content").Attribute("id", "content").Class("uk-textarea").Append(input.Content))
        
      );
    
    // Check Grammar button
    var checkGrammarButton = Button.Class("uk-button uk-button-primary")
                              .Attribute("id", "checkBtn")
                              .Attribute("type", "button") 
                              .Append("Check Grammar & Style");

    var checkGrammarDiv = Div
                          .Style("margin-bottom", "20px")
                          .Append(checkGrammarButton);

    var suggestionsDiv = Div.Id("suggestions")
                            .Style("margin-bottom", "20px");

    var attachmentField = Div
      .Append(Label.Class("uk-form-label").Append(nameof(input.Attachment)))
      .Append(Div.Attribute("uk-form-custom", "target: true")
        .Append(Input.File.Name("Attachment"))
        .Append(Input.Text.Class("uk-input uk-form-width-large").Attribute("placeholder", "Click to select file").ToggleAttribute("disabled", true))
      );

    if (modelState is object && !modelState.IsValid)
    {
        if (IsFieldOK("Name"))
        {
            foreach (var er in modelState["Name"]!.Errors)
            {
                nameField = nameField.Append(Div.Class("uk-form-danger uk-text-small").Append(er.ErrorMessage));
            }
        }

        if (IsFieldOK("Content"))
        {
            foreach (var er in modelState["Content"]!.Errors)
            {
                var errorDiv = Div.Class("uk-form-danger uk-text-small").Append(er.ErrorMessage).Style("margin-bottom", "20px");
                var tempContent = Div.Append(errorDiv).Append(contentField);
                contentField = tempContent;
            }
        }
    }

    var submit = Div.Style("margin-top", "20px")
                 .Style("margin-bottom", "20px")
                 .Append(Button.Class("uk-button uk-button-primary")
                 .Append("Submit"))
                 .Append(Hr);

    var form = Form
               .Class("uk-form-stacked")
               .Attribute("method", "post")
               .Attribute("enctype", "multipart/form-data")
               .Attribute("action", $"/{path}")
                 .Append(antiForgeryField)
                 .Append(tagsButton)
                 .Append(tagsDiv)
                 .Append(nameField)
                 .Append(contentField)
                 .Append(checkGrammarDiv)
                 .Append(suggestionsDiv)
                 .Append(attachmentField);

    if (input.Id is object)
    {
        HtmlTag id = Input.Hidden.Name("Id").Value(input.Id.ToString()!);
        form = form.Append(id);
    }

    form = form.Append(submit);

    return form.ToHtmlString();
}

class Render
{
    static string KebabToNormalCase(string txt) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(txt.Replace('-', ' '));

    static string[] MarkdownEditorHead() => new[]
    {
      @"<link rel=""stylesheet"" href=""https://unpkg.com/easymde/dist/easymde.min.css"">",
      @"<script src=""https://unpkg.com/easymde/dist/easymde.min.js""></script>"
    };

    static string[] MarkdownEditorFoot() => new[]
    {
      @"<script>
        var easyMDE = new EasyMDE({
          insertTexts: {
            link: [""["", ""]()""]
          }
        });

        function copyMarkdownLink(element) {
          element.select();
          document.execCommand(""copy"");
        }
  document.getElementById('checkBtn').addEventListener('click', async (event) => {
    event.preventDefault();
    var language = ""en-US"";
    const content = easyMDE.value();

    fetch('https://api.languagetool.org/v2/check', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: new URLSearchParams({
            text: content,
            language: language
        })
    })
    .then(response => response.json())
    .then(data => {
        const suggestionsDiv = document.getElementById('suggestions');
        suggestionsDiv.innerHTML = '';

        if (data.matches && data.matches.length > 0) {
            data.matches.forEach(match => {
                const suggestion = document.createElement('div');
                var replacements = match.replacements.map(r => r.value).join(', ');
                if(replacements == ' ') {
             replacements = 'No replacements found'
            }
                    suggestion.innerHTML = `
                    <p><strong>Mistake:</strong> ${match.message}</p>
                    <pre style=""white-space: pre-wrap;"">${match.context.text}</pre>
                    <p><strong>Replacements:</strong> ${replacements}</p>
                    <hr>
                `;
                suggestionsDiv.appendChild(suggestion);
            });
        } else {
            suggestionsDiv.innerHTML = ""No suggestions found."";
        }
    })
    .catch(error => {
        console.error('LanguageTool API request failed:', error);
    });
});


document.getElementById('tags').addEventListener('click', async () => {
const content = easyMDE.value();
const endpoint = '/get-tags'; 

  const headers = {
    'Content-Type': 'application/json'
  };

  const body = JSON.stringify({
    texts: [content]
  });

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: headers,
      body: body
    });

    if (!response.ok) {
      throw new Error(`HTTP error! Status: ${response.status}`);
    }

    const data = await response.json();
const classifications = data[0].classification;
    let highestPercentageTag = '';
    let highestPercentage = 0;

    for (const classification of classifications) {
      if (classification.p > highestPercentage) {
        highestPercentage = classification.p;
        highestPercentageTag = classification.className;
      }
    }
if(highestPercentage <= 0.2) {
highestPercentageTag = 'No tags found';
}
  const tagsDiv = document.getElementById('classification');
            tagsDiv.innerHTML = '';
const tag = document.createElement('div');
                tag.innerHTML = `<strong>${highestPercentageTag}<br><br></strong>`;
                tagsDiv.appendChild(tag);
    
  } catch (error) {
    console.error('Error fetching tags:', error);
  }
});

        </script>"
    };
    
    (Template head, Template body, Template layout) _templates = (
      head: Scriban.Template.Parse(
        """
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{ title }}</title>
            <link rel='icon' type='image/svg' href='\wiki-unblocked-svgrepo-com.svg'>
          <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/uikit@3.19.4/dist/css/uikit.min.css" />
          {{ header }}
          <style>
            .last-modified { font-size: small; }
            a:visited { color: blue; }
            a:link { color: red; }
            .custom-button-height {
             width: 100%;
             max-width: 230px;
          }
          a.custom {
              color: blue;
          text-transform: none;
          }
          .delete-btn{
           width: 100%;
            min-width: 140px;
          max-width: 200px;
          }
           .uk-navbar-container {
              background-color: #fff;
              border-bottom: 1px solid #e5e5e5;
             height: 99px; /* Adjust this value to increase the height */
          display: flex;
          align-items: center;
          }
          .uk-navbar-center {
              flex-grow: 1;
          }
          .uk-navbar-item form {
              display: flex;
              align-items: center;
          }
          .black-icon {
              color: black 
          }
          @media (max-width: 430px) {
              .uk-navbar-center .uk-navbar-item form {
                  flex-direction: column;
                  align-items: flex-start;
              }
              .uk-navbar-item .uk-input {
                  margin-right: 0;
                  margin-bottom: 10px;
                  width: 100%;
                margin-right: 10px;
              flex-grow: 1;
              }
              .uk-navbar-item .uk-button {
                  width: 100%;
              }
                 .uk-navbar-left {
              margin-left: 20px; /* Adjust this value to increase the left margin */
          }
          }
          
          </style>
          """),
      body: Scriban.Template.Parse(""""
          <nav class="uk-navbar-container uk-margin-bottom" uk-navbar>
              <div class="uk-navbar-left">
                  <a href="/" class="uk-navbar-item uk-logo uk-margin-left">
                      <span uk-icon="icon: home" class="black-icon"></span>
                  </a>
              </div>
              <div class="uk-navbar-center">
                  <div class="uk-navbar-item">
                      <form id="newPageForm" action="/new-page">
                          <input class="uk-input" type="text" name="pageName" id ="pageName" placeholder="Type desired page title" required>
                          <input type="submit"  class="uk-button uk-button-default" value="Add New Page">
                      </form>
                  </div>
              </div>
          </nav>
           {{ if at_side_panel != "" }}
                  <div class="uk-container">
                  <div uk-grid>
                    <div class="uk-width-4-5">
                      <h1>{{ page_name }}</h1>
                      {{ content }}
                    </div>
                    <div class="uk-width-1-5">
                      {{ at_side_panel }}
                    </div>
                  </div>
                  </div>
                {{ else }}
                  <div class="uk-container">
                    <h1>{{ page_name }}</h1>
                    {{ content }}
                  </div>
                {{ end }}
              <footer class="uk-section uk-section-small uk-section-muted" uk-sticky="position: bottom"">
                <div class="uk-container">
                  <div class="uk-text-center">
            <p>&copy; 2024 Wiki. All rights reserved.</p>
          </div>
                </div>
              </footer>   
                <script src="https://cdn.jsdelivr.net/npm/uikit@3.19.4/dist/js/uikit.min.js"></script>
                <script src="https://cdn.jsdelivr.net/npm/uikit@3.19.4/dist/js/uikit-icons.min.js"></script>
                <script>
                document.getElementById('newPageForm').addEventListener('submit', function(event) {
              event.preventDefault(); // Prevent form submission

              const pageName = document.getElementById('pageName').value.trim();

              // Perform an AJAX request to check if the page name exists
              fetch(`/check-page-exists?pageName=${encodeURIComponent(pageName)}`)
                  .then(response => response.json())
                  .then(data => {
                      if (data.exists) {
                          UIkit.notification({message: 'Page name already exists!', status: 'danger'});
                      } else {
                          // If the page name doesn't exist, proceed with form submission
                          this.submit();
                      }
                  })
                  .catch(error => {
                      console.error('Error checking page name:', error);
                      UIkit.notification({message: 'An error occurred while checking the page name.', status: 'danger'});
                  });
          });
                </script>
               
                {{ at_foot }}
          """"),
      layout: Scriban.Template.Parse("""
                <!DOCTYPE html>
                  <head>
                    {{ head }}
                  </head>
                  <body>
                    {{ body }}
                  </body>
                </html>
          """)
    );

    // Use only when the page requires editor
    public HtmlString BuildEditorPage(string title, Func<IEnumerable<string>> atBody, Func<IEnumerable<string>>? atSidePanel = null) =>
      BuildPage(
        title,
        atHead: () => MarkdownEditorHead(),
        atBody: atBody,
        atSidePanel: atSidePanel,
        atFoot: () => MarkdownEditorFoot()
        );
 
    // General page layout building function
    public HtmlString BuildPage(string title, Func<IEnumerable<string>>? atHead = null, Func<IEnumerable<string>>? atBody = null, Func<IEnumerable<string>>? atSidePanel = null, Func<IEnumerable<string>>? atFoot = null)
    {
        var head = _templates.head.Render(new
        {
            title,
            header = string.Join("\r", atHead?.Invoke() ?? new[] { "" })
        });

        var body = _templates.body.Render(new
        {   
            PageName = KebabToNormalCase(title),
            Content = string.Join("\r", atBody?.Invoke() ?? new[] { "" }),
            AtSidePanel = string.Join("\r", atSidePanel?.Invoke() ?? new[] { "" }),
            AtFoot = string.Join("\r", atFoot?.Invoke() ?? new[] { "" })
        });

        return new HtmlString(_templates.layout.Render(new { head, body }));
    }
}

class Wiki
{
    DateTime Timestamp() => DateTime.UtcNow;

    const string PageCollectionName = "Pages";
    const string AllPagesKey = "AllPages";
    const double CacheAllPagesForMinutes = 30;

    readonly IWebHostEnvironment _env;
    readonly IMemoryCache _cache;
    readonly ILogger _logger;

    public Wiki(IWebHostEnvironment env, IMemoryCache cache, ILogger<Wiki> logger)
    {
        _env = env;
        _cache = cache;
        _logger = logger;
    }

    // Get the location of the LiteDB file.
    string GetDbPath() => Path.Combine(_env.ContentRootPath, "wiki.db");

    // List all the available wiki pages. It is cached for 30 minutes.
    public List<Page> ListAllPages()
    {
        var pages = _cache.Get(AllPagesKey) as List<Page>;

        if (pages is object)
            return pages;

        using var db = new LiteDatabase(GetDbPath());
        var coll = db.GetCollection<Page>(PageCollectionName);
        var items = coll.Query().ToList();

        _cache.Set(AllPagesKey, items, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheAllPagesForMinutes)));
        return items;
    }

    // Get a wiki page based on its path
    public Page? GetPage(string path)
    {
        using var db = new LiteDatabase(GetDbPath());
        var coll = db.GetCollection<Page>(PageCollectionName);
        coll.EnsureIndex(x => x.Name);

        return coll.Query()
                .Where(x => x.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
    }

    // Save or update a wiki page. Cache(AllPagesKey) will be destroyed.
    public (bool isOk, Page? page, Exception? ex) SavePage(PageInput input)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var coll = db.GetCollection<Page>(PageCollectionName);
            coll.EnsureIndex(x => x.Name);

            Page? existingPage = input.Id.HasValue ? coll.FindOne(x => x.Id == input.Id) : null;

            var sanitizer = new HtmlSanitizer();
            var properName = input.Name.ToString().Trim().Replace(' ', '-').ToLower();

            Attachment? attachment = null;
            if (!string.IsNullOrWhiteSpace(input.Attachment?.FileName))
            {
                attachment = new Attachment
                (
                    FileId: Guid.NewGuid().ToString(),
                    FileName: input.Attachment.FileName,
                    MimeType: input.Attachment.ContentType,
                    LastModifiedUtc: Timestamp()
                );

                using var stream = input.Attachment.OpenReadStream();
                var res = db.FileStorage.Upload(attachment.FileId, input.Attachment.FileName, stream);
            }

            if (existingPage is not object)
            {
                var newPage = new Page
                {
                    Name = sanitizer.Sanitize(properName),
                    Content = input.Content, //Do not sanitize on input because it will impact some markdown tag such as >. We do it on the output instead.
                    LastModifiedUtc = Timestamp()
                };

                if (attachment is object)
                    newPage.Attachments.Add(attachment);

                coll.Insert(newPage);

                _cache.Remove(AllPagesKey);
                return (true, newPage, null);
            }
            else
            {
                var updatedPage = existingPage with
                {
                    Name = sanitizer.Sanitize(properName),
                    Content = input.Content, //Do not sanitize on input because it will impact some markdown tag such as >. We do it on the output instead.
                    LastModifiedUtc = Timestamp()
                };

                if (attachment is object)
                    updatedPage.Attachments.Add(attachment);

                coll.Update(updatedPage);

                _cache.Remove(AllPagesKey);
                return (true, updatedPage, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"There is an exception in trying to save page name '{input.Name}'");
            return (false, null, ex);
        }
    }

    public (bool isOk, Page? p, Exception? ex) DeleteAttachment(int pageId, string id)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var coll = db.GetCollection<Page>(PageCollectionName);
            var page = coll.FindById(pageId);
            if (page is not object)
            {
                _logger.LogWarning($"Delete attachment operation fails because page id {id} cannot be found in the database");
                return (false, null, null);
            }

            if (!db.FileStorage.Delete(id))
            {
                _logger.LogWarning($"We cannot delete this file attachment id {id} and it's a mystery why");
                return (false, page, null);
            }

            page.Attachments.RemoveAll(x => x.FileId.Equals(id, StringComparison.OrdinalIgnoreCase));

            var updateResult = coll.Update(page);

            if (!updateResult)
            {
                _logger.LogWarning($"Delete attachment works but updating the page (id {pageId}) attachment list fails");
                return (false, page, null);
            }

            return (true, page, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex);
        }
    }

    public (bool isOk, Exception? ex) DeletePage(int id, string homePageName)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var coll = db.GetCollection<Page>(PageCollectionName);

            var page = coll.FindById(id);

            if (page is not object)
            {
                _logger.LogWarning($"Delete operation fails because page id {id} cannot be found in the database");
                return (false, null);
            }

            if (page.Name.Equals(homePageName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"Page id {id}  is a home page and delete operation on home page is not allowed");
                return (false, null);
            }

            //Delete all the attachments
            foreach (var a in page.Attachments)
            {
                db.FileStorage.Delete(a.FileId);
            }

            if (coll.Delete(id))
            {
                _cache.Remove(AllPagesKey);
                return (true, null);
            }

            _logger.LogWarning($"Somehow we cannot delete page id {id} and it's a mistery why.");
            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    // Return null if file cannot be found.
    public (LiteFileInfo<string> meta, byte[] file)? GetFile(string fileId)
    {
        using var db = new LiteDatabase(GetDbPath());

        var meta = db.FileStorage.FindById(fileId);
        if (meta is not object)
            return null;

        using var stream = new MemoryStream();
        db.FileStorage.Download(fileId, stream);
        return (meta, stream.ToArray());
    }
}

record Page
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime LastModifiedUtc { get; set; }

    public List<Attachment> Attachments { get; set; } = new();
}

record Attachment
(
    string FileId,

    string FileName,

    string MimeType,

    DateTime LastModifiedUtc
);

record PageInput(int? Id, string Name, string Content, IFormFile? Attachment)
{
    public static PageInput From(IFormCollection form)
    {
        var (id, name, content) = (form["Id"], form["Name"], form["Content"]);

        int? pageId = null;

        if (!StringValues.IsNullOrEmpty(id))
            pageId = Convert.ToInt32(id);

        IFormFile? file = form.Files["Attachment"];

        return new PageInput(pageId, name!, content!, file);
    }
}

class PageInputValidator : AbstractValidator<PageInput>
{
    public PageInputValidator(string pageName, string homePageName)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        if (pageName.Equals(homePageName, StringComparison.OrdinalIgnoreCase))
            RuleFor(x => x.Name).Must(name => name.Equals(homePageName)).WithMessage($"You cannot modify home page name. Please keep it {homePageName}");

        RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required");
    }
}
