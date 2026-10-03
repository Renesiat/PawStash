# Auto-fill plan

**Status:** decisions 1–6 and 14 answered by Alina on 2026-10-02, decision 15 on 2026-10-03. Decisions 7–13 were
proposed with this plan; she answered its open question (A) without changing them. Stages 1–3 are integrated (section
7). On 2026-10-03 editing lost the content fields (file-system plan, decision 46), so auto-fill now runs only when an
item is created; decision 5 no longer applies. Next come Alina's review and manual tests.
**Updated:** 2026-10-03

UI strings are quoted exactly as they appear in the app (Ukrainian).

## 1. Goal and scope

When an item is created, its name, description and cover image are filled from its content. Alina sees the filled
fields in the form and can change them before `Зберегти`.

| Type | Name | Description | Cover image |
|---|---|---|---|
| Link | The page's title | The page's description | The page's preview picture; without one, the site's icon |
| Photo | The file name | — | The photo itself, reduced |
| Document | The file name | — | PDF: the first picture inside it, small icons skipped. txt, md, csv, json: none, the type icon stays |

**In scope:**

- auto-fill in `ItemPage` when a link's address is entered and when a file is picked;
- `Файл` in the `+` menu opens `ItemPage` for one file (decision 3);
- covers made by the server from photos and PDFs, on create (the file can't be replaced later: file-system plan,
  decision 46).

**Out of scope:** existing items (they stay as they are), refreshing a link's data later, covers from Office files,
OCR, descriptions for photos and documents.

## 2. Decisions

**Answered by Alina, 2026-10-02:**

| # | Question | Decision |
|---|---|---|
| 1 | Cover image of a link | The page's preview picture (the one messengers show for a link); without one, the site's icon |
| 2 | Cover image of a PDF | The first picture inside the PDF, small icons skipped; without one, the document icon stays |
| 3 | `Файл` in the `+` menu | After a file is picked, `ItemPage` opens with the fields already filled, so they can be checked before saving. Files are added one at a time |
| 4 | What auto-fill leaves alone | It fills only empty fields and never overwrites what Alina typed or picked. (Its part about a new link address when editing no longer applies: since 2026-10-03 the address can't change, file-system plan, decision 46) |
| 5 | Replacing the file when editing | **No longer applies (2026-10-03):** the file can't be replaced (file-system plan, decision 46). It was: the cover image is made again from the new file, if the new file has a picture and no other cover image was picked in the same form |
| 6 | Who does what | The app reads link pages, as the phone's browser would; the server never requests other sites. The server makes covers from photos and PDFs, with two new libraries: ImageSharp for pictures and PdfPig for reading PDFs |

**Proposed with this plan:**

| # | Question | Proposal |
|---|---|---|
| 7 | Existing items | Not changed |
| 8 | A page that can't be read (no internet, the site refuses) | The link is saved as today: the name is the site's address, no description or cover |
| 9 | What "empty" means in decision 4 | A field counts as empty while it is empty or still holds what auto-fill put there. Pasting another address updates what auto-fill wrote, never what Alina typed or picked |
| 10 | `Прибрати` on an auto-filled cover in the create form | The create request gets `removeCoverImage = true`, so the server makes no cover from the file (section 6) |
| 11 | Cover image size | Every cover the server stores is reduced to fit 512 × 512 px: JPEG (quality 85), or PNG when it has transparency. This covers pictures made from files, link pictures and pictures from the gallery, so folder lists load faster |
| 12 | ImageSharp version | 3.1.12, the latest 3.x. From 4.0 the build checks for a `sixlabors.lic` licence file. Licence: Six Labors Split License, free (Apache 2.0 terms) for individuals and companies under $1M annual revenue |
| 13 | A picked file that can't be added | As today, but for one file: «Файл не додано» with the reason |

**Answered by Alina, 2026-10-02 (the open question of this plan):**

| # | Question | Decision |
|---|---|---|
| 14 | A PDF's cover image in the form before saving (decisions 3 and 6 mean the server makes it while saving) | Grey text under `Обкладинка`: «Якщо в PDF є картинка, вона стане обкладинкою після збереження». The result shows in the list and in `Редагувати` |

**Answered by Alina, 2026-10-03 (found during integration):**

| # | Question | Decision |
|---|---|---|
| 15 | The User-Agent for reading link pages | A neutral one, like link-preview bots use: `Mozilla/5.0 (compatible; PawStash/1.0)`. Checked on YouTube, Instagram, Wikipedia and GitHub: all of them give the title and picture in `<head>`. With a mobile browser's User-Agent (the first version of section 3), YouTube sends a mobile page with the video's title outside `<head>`, and Instagram sends nothing |

## 3. Links (the app)

1. **When.** In `ItemPage` for a link, the page is read once the address passes `FileSystemItemRules.ValidateLinkUrl`
   and hasn't changed for 1 second. Pasting counts. Only when creating: the edit form has no address field
   (file-system plan, decision 46).
2. **What Alina sees.** While the page is read, grey text under the address: «Отримую дані зі сторінки…». If reading
   fails: grey «Не вдалося отримати дані зі сторінки». Saving works in both cases.
3. **Reading the page:**
   - its own `HttpClient`, never the API client, so the `X-User-Email` header never goes to other sites;
   - redirects are followed; timeout 10 s; only `text/html` is read, at most the first 1 MB, stopping at `</head>`;
   - the request carries a neutral User-Agent, `Mozilla/5.0 (compatible; PawStash/1.0)` (decision 15);
   - the text is decoded with the page's charset, UTF-8 by default.
4. **What is taken.** No HTML library: the tags are found in the text, HTML entities are decoded, and relative
   addresses are resolved against the page's final address.

   | Field | Tags, the first one found wins | Limit |
   |---|---|---|
   | Name | `og:title`, `twitter:title`, `<title>` | Spaces collapsed, cut to 255 characters |
   | Description | `og:description`, `twitter:description`, `description` | Cut to 2000 characters |
   | Cover image | `og:image`, `og:image:secure_url`, `twitter:image`; then `apple-touch-icon`; then a PNG `icon` | The first picture that downloads as jpg, png, webp or gif, up to 5 MB (the cover rules). Others (svg, ico, avif) are skipped |

5. **Filling** follows decisions 4 and 9. The picture goes to the server as the usual `coverImage`, so links need
   no server changes besides decision 11.
6. **Saving while the page is still being read** stops the reading, and the form is saved as it is.

## 4. Files: photos and documents

**In the app:**

1. `Файл` opens the system file picker for one file, with the same formats as today.
2. The type comes from the extension, and the file is checked (format, size) as today. A problem shows as «Файл не
   додано» with the reason (decision 13).
3. `ItemPage` opens in create mode for that type, with the file:
   - `Файл`: «Новий файл: sunset.png» and `Замінити файл`;
   - `Назва`: the file name;
   - `Обкладинка`: a photo shows itself (the server stores a reduced copy on save); a PDF shows the grey text from
     decision 14; a text file shows nothing;
   - `Опис`: empty.
4. `Зберегти` sends the file, name and description. A cover image is sent only if Alina picked one from the
   gallery. `Прибрати` sends `removeCoverImage = true` (decision 10). Otherwise the server makes the cover from the
   file.
5. `Замінити файл` exists only in the create form, to pick a different file before saving. The edit form has no
   file field (file-system plan, decision 46).

**On the server** (`FileSystemService`, with a new helper `CoverImageMaker`):

| Request | `coverImage` | `removeCoverImage` | Cover image |
|---|---|---|---|
| Create a photo or document | sent | — | The sent picture, reduced (decision 11) |
| Create a photo or document | — | `true` | None |
| Create a photo or document | — | — | Made from the file, if it has a picture |
| Edit | sent | — | The sent picture, reduced |
| Edit | — | `true` | Removed |
| Edit | — | — | Unchanged (the edit request carries no file: file-system plan, decision 46) |

**Making the cover:**

- **Photo:** ImageSharp decodes it straight to a small size (cheap even for big photos), turns it upright using
  the camera's rotation (EXIF), fits it in 512 × 512 and saves it as in decision 11.
- **PDF:** PdfPig looks through the first 10 pages for the first picture of at least 100 × 100 px. ImageSharp then
  reduces it the same way. Pictures it can't read are skipped.
- **txt, md, csv, json:** no cover.
- **If anything fails,** the item is saved without a cover. Auto-fill never blocks a save.
- **Storage.** A made cover is stored like any cover image (`{item_id}-cover-{random}.jpg`), so it can be replaced
  or removed like one. No database changes.

## 5. Where things live

| Layer | Files | Purpose |
|---|---|---|
| `PawStash.BLL` | `Interfaces/ICoverImageMaker.cs`, `Implementations/CoverImageMaker.cs` | Makes a cover from a photo or a PDF and reduces any cover (ImageSharp 3.1.12, PdfPig 0.1.16) |
| | `Implementations/FileSystemService.cs` | Uses it on create and edit, as in section 4 |
| `PawStash.API` | `Models/FileSystemItemPostForm.cs` | Gets `RemoveCoverImage` |
| `PawStash.Common` | `Parsers/LinkPageParser.cs`, `Models/LinkPageInfo.cs` | Finds the title, description and picture addresses in a page's HTML. In Common, so the automated tests can reach it |
| `PawStash.App` | `ItemPage` and `ItemViewModel`, `BottomBar`, a service that reads link pages | The form, `Файл`, reading pages |

## 6. Changes to the file-system plan

Applied to `docs/file-system-integration-plan.md` on 2026-10-02, with the go-ahead:

- **Section 1, out of scope:** "server-side thumbnails" are now in scope for cover images (this plan).
- **Section 5, forms:** `removeCoverImage` also on create: `true` means no cover image, so none is made from the
  file.
- **Section 8, `Файл`:** one file, then `ItemPage` (decision 3 here) instead of several files sent at once.
- **Decision 13** ("Several files at once: one create request per file"): replaced by decision 3 here.

## 7. Stages

Each stage ends with a working version, checked through Swagger and by clicking through the app.

| # | Stage | What appears |
|---|---|---|
| 1 | **Server: cover images** | `CoverImageMaker`, the rules from section 4, decision 11, `removeCoverImage` on create |
| 2 | **App: files through the form** | `Файл` opens `ItemPage` with the picked file; the photo shown as its cover; `Прибрати`, the gallery and `Замінити файл` as in section 4 |
| 3 | **App: links** | Reading pages and filling the form (section 3) |

**Progress (2026-10-03):** stages 1–3 are done.

- **Stage 1** was checked over HTTP:
  - covers made from a photo turned by the camera (EXIF), PNG with transparency, GIF, WebP and a PDF with a
    picture;
  - a PDF with only a small logo, a text-only PDF and a txt file get no cover;
  - a broken picture saves without a cover;
  - `removeCoverImage` works on create;
  - «Або нова картинка, або видалення» on create and edit;
  - sent covers are reduced;
  - replacing a file remakes the cover, or keeps it when the new PDF has no picture (removed on 2026-10-03 with
    decision 46 of the file-system plan);
  - the old cover files are removed from disk.
- **Stages 2 and 3** were checked on the emulator:
  - `Файл` opens the filled form; a photo shows itself as the cover, a PDF shows the decision 14 text;
  - the saved covers appear in the list;
  - `Прибрати` before saving gives no cover;
  - a YouTube link fills the name, description and picture;
  - after another address, a typed name stays while auto-filled fields update;
  - when editing, a new address fills only the empty cover (removed on 2026-10-03: the address can't change).

## 8. Delivery process

As in the file-system plan, section 10: plan → this document → integration → fixes → Alina's manual tests →
automated tests. The automated tests cover cover-making in `FileSystemService` and `LinkPageParser`.

## 9. Open questions

None. The PDF question is answered as decision 14 (option A).
