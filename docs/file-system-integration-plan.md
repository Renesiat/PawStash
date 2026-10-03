# File system integration plan

**Status:** revised on 2026-10-02 for one general item endpoint (Alina's request). Decisions are in
section 11; questions from stage 3 wait in section 12. Stages 0–1 are done. The app is mobile only for now
(decisions 33–37): Android, checked on the emulator. Stages 1a, 2 and 3 are done too. Next: stage 4. On 2026-10-03
editing was limited to the name, description and cover image (decision 46).
**Updated:** 2026-10-03

UI strings are quoted exactly as they appear in the app (Ukrainian).

## 1. Goal and scope

A file manager like Windows Explorer: folders nested to any depth. **Every item is one kind of object:**

| Part | Required | Notes |
|---|---|---|
| Type | yes | `folder`, `link`, `note`, `photo`, `document` |
| Content | yes, except for folders | link: a URL; note: text; photo: an image file; document: a PDF or text file. Set on create and can't change later (decision 46) |
| Name | no | a default is used when empty (section 7) |
| Description | no | |
| Cover image | no | a custom picture shown instead of the type icon |

**One general endpoint** creates and edits any item. The client says which type it is, and the server
checks that the content matches that type.

Links are expected to be the most common item, so they come first in the app.

**In scope:**

- create, open, edit, move, delete;
- a path at the top (`Головна › Рецепти › Супи`), every segment clickable;
- sorting: folders first, then files; by name, modified date or type.

**Out of scope (next steps):** custom fields, tags, search, OCR, design beyond the bottom bar, trash, copying, drag and drop,
multi-select, offline mode, sharing between users, Office formats (docx, xlsx). Cover images made from files (server-side
thumbnails) were out of scope here too; they now come with [`auto-fill-plan.md`](auto-fill-plan.md).

## 2. How the file system maps onto the database

- **One table, `file_system_items`,** holds every item. The type is in `item_type`. In code these are
  classes with the common base `FileSystemItem`, stored in one table by EF Core (table-per-hierarchy):

  ```
  FileSystemItem   (name, description, cover image)
  ├── Folder
  ├── Link         (URL)
  ├── Note         (text)
  └── UploadedFile (file)
      ├── Photo
      └── Document
  ```

  Why one table:
  - a name is unique within a folder across all types, and one index enforces it;
  - every operation (create, edit, move, delete) works the same way for every type;
  - a folder's contents come back in one query.
- **Tree.** Each item knows only the folder it is in (`parent_folder_id`), so a move changes one field.
  Code computes the path and checks that a folder is not being moved into itself: it reads the user's
  folder tree in one query and walks it in memory.
- **No real folders on disk.** The structure lives in the database. Uploaded files and cover images are
  stored on the server's disk, and the database keeps their paths.

## 3. Database schema

EF migrations create the table from the entity configurations. The SQL below shows the target table.

```sql
CREATE TABLE file_system_items
(
    item_id               uuid          NOT NULL,
    owner_email           varchar(254)  NOT NULL,
    parent_folder_id      uuid          NULL,
    item_type             varchar(10)   NOT NULL,
    name                  varchar(255)  NOT NULL,
    name_lowercase        varchar(255)  GENERATED ALWAYS AS (lower(name)) STORED,
    description           text          NULL,
    cover_image_path      varchar(300)  NULL,
    cover_image_mime_type varchar(100)  NULL,
    created_at            timestamptz   NOT NULL,
    updated_at            timestamptz   NOT NULL,
    link_url              varchar(2048) NULL,
    note_text             text          NULL,
    file_path             varchar(300)  NULL,
    file_mime_type        varchar(100)  NULL,
    file_size_bytes       bigint        NULL,
    CONSTRAINT pk_file_system_items               PRIMARY KEY (item_id),
    CONSTRAINT fk_file_system_items_owner         FOREIGN KEY (owner_email)      REFERENCES users (email)                ON DELETE CASCADE,
    CONSTRAINT fk_file_system_items_parent_folder FOREIGN KEY (parent_folder_id) REFERENCES file_system_items (item_id) ON DELETE CASCADE,
    CONSTRAINT ck_file_system_items_item_type     CHECK (item_type IN ('folder', 'link', 'note', 'photo', 'document')),
    CONSTRAINT ck_file_system_items_link_url      CHECK (item_type <> 'link' OR link_url IS NOT NULL),
    CONSTRAINT ck_file_system_items_note_text     CHECK (item_type <> 'note' OR note_text IS NOT NULL),
    CONSTRAINT ck_file_system_items_uploaded_file CHECK (item_type NOT IN ('photo', 'document')
                                                         OR (file_path IS NOT NULL AND file_mime_type IS NOT NULL AND file_size_bytes IS NOT NULL)),
    CONSTRAINT ck_file_system_items_cover_image   CHECK ((cover_image_path IS NULL) = (cover_image_mime_type IS NULL))
);

CREATE UNIQUE INDEX uq_file_system_items_name_in_folder
    ON file_system_items (owner_email, parent_folder_id, name_lowercase) NULLS NOT DISTINCT;

CREATE INDEX ix_file_system_items_parent_folder_id
    ON file_system_items (parent_folder_id);
```

What matters here:

- **`ON DELETE CASCADE` on `parent_folder_id`.** Deleting a folder removes everything inside it in one
  statement.
- **The database computes `name_lowercase`,** so "Фото" and "фото" count as the same name.
  `NULLS NOT DISTINCT` applies the same rule at the root.
- **`CHECK` constraints.** The database refuses a link without a URL, even if the code has a bug.
- **`owner_email`.** Each email has its own file system.
- **`description` and `cover_image_*` are common to every type.** The earlier `link_description` is
  replaced by `description`.
- **Only a folder can be a parent.** The service checks this.

**Naming rules:**

- everything lowercase, words separated by `_`;
- primary key named `<entity>_id`;
- columns used by one kind of item start with its name (`link_`, `note_`), and with `file_` for uploaded
  files;
- `item_type` values are lowercase too;
- constraints and indexes are prefixed `pk_`, `fk_`, `ck_`, `uq_`, `ix_`.

**Migrations:**

- Applied locally so far: `CreateUsersTable` and `CreateFileSystemItemsTable` (the whole table, before
  this revision).
- This revision adds one more: `AddDescriptionAndCoverImage` (adds `description` and the
  `cover_image_*` columns, drops `link_description`).

## 4. Where things live

| Layer | Files | Purpose |
|---|---|---|
| `PawStash.Common` | `Enums/FileSystemItemType.cs` | `Folder`, `Link`, `Note`, `Photo`, `Document`. Sent in JSON as a lowercase string (`"folder"`) |
| | `Rules/FileSystemItemRules.cs` | The rules from section 7, shared by the server and the app |
| | `Models/DTO/FileSystem/*.cs` | The DTOs from section 5 |
| `PawStash.DAL` | `Entities/FileSystem/*.cs` | `FileSystemItem`, `Folder`, `Link`, `Note`, `UploadedFile`, `Photo`, `Document` |
| | `EntityTypeConfigurations/FileSystem/*.cs` | Table, keys, indexes, `CHECK` constraints, column names |
| | `Context/PawStashContext.cs` | `DbSet`, plus `created_at` / `updated_at` set automatically on save |
| `PawStash.BLL` | `Interfaces/ICurrentUser.cs`, `Implementations/CurrentUser.cs` | Email of whoever is making the request |
| | `Interfaces/IFileSystemService.cs`, `Implementations/FileSystemService.cs` | All the logic from section 7, in one service |
| | `Models/FileSystemItemInput.cs` | What create / edit receives: type, content, name, description, cover image |
| | `Validators/FileSystem/*Validator.cs` | FluentValidation on top of `FileSystemItemRules`; content rules depend on the type |
| | `Interfaces/IFileStorage.cs`, `Implementations/LocalFileStorage.cs` | Saving, reading and deleting uploaded files and cover images |
| | `Results/ServiceResult.cs` | Service results with and without data (delete returns no data) |
| | `Mappers/FileSystemItemMapper.cs` | Entity → DTO as LINQ projections, so folder listings never load note text |
| | `Models/FileUpload.cs`, `Models/FileContent.cs`, `Models/FolderNode.cs` | A file coming in, a file going out, a folder in the in-memory tree |
| | `Validators/Extensions/RuleBuilderExtensions.cs` | `Satisfies(...)`: plugs the `FileSystemItemRules` checks into FluentValidation |
| `PawStash.API` | `Filters/AllowedEmailFilter.cs`, `Filters/AllowWithoutEmailAttribute.cs` | Checks `X-User-Email` on every request except login; 401 without it |
| | `Controllers/FileSystemItemsController.cs` | Every endpoint from section 6 |
| | `Models/FileSystemItemPostForm.cs`, `FileSystemItemPutForm.cs` | The multipart forms for create and edit |
| `PawStash.App` | `Controls/BottomBar.xaml` | The floating bottom bar with tabs and the `+` button (section 8) |
| | `Services/ApiClient.cs` | Adds the email header, parses errors, sends the user back to login on 401 |
| | `Services/FileSystemApi.cs` | One method per API request |
| | `Views/` + `ViewModels/` | The pages from section 8 |

## 5. Models

**Entities** (`PawStash.DAL/Entities/FileSystem`):

```csharp
public abstract class FileSystemItem
{
    public Guid ItemId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public Folder? ParentFolder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameLowercase { get; private set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImagePath { get; set; }
    public string? CoverImageMimeType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Folder : FileSystemItem { public List<FileSystemItem> Items { get; set; } = []; }
public class Link : FileSystemItem { public string Url { get; set; } = string.Empty; }
public class Note : FileSystemItem { public string Text { get; set; } = string.Empty; }

public abstract class UploadedFile : FileSystemItem
{
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class Photo : UploadedFile { }
public class Document : UploadedFile { }
```

**DTOs** (`PawStash.Common/Models/DTO/FileSystem`):

| DTO | Fields | Used for |
|---|---|---|
| `FileSystemItemDto` | `ItemId`, `ParentFolderId`, `ItemType`, `Name`, `HasCoverImage`, `UpdatedAt`, `LinkUrl?`, `FileSizeBytes?` | A row in a folder listing |
| `FileSystemItemDetailsDto` | The above, plus `Description`, `CreatedAt`, `NoteText?`, `FileMimeType?` | An opened item |
| `FolderPathItemDto` | `ItemId`, `Name` | One segment of the path at the top |
| `ItemParentFolderPutDto` | `TargetFolderId` (`null` = root) | Move |

**Forms** (`multipart/form-data`, because they can carry files):

| Field | Create (`POST`) | Edit (`PUT`) |
|---|---|---|
| `itemType` | required | — (the type can't change) |
| `parentFolderId` | optional (empty = root) | — (use move) |
| `name` | optional | optional (empty = default from section 7) |
| `description` | optional | optional (empty = no description) |
| `linkUrl` | required for a link | — (content can't change, decision 46) |
| `noteText` | required for a note | — (content can't change, decision 46) |
| `file` | required for a photo or document | — (content can't change, decision 46) |
| `coverImage` | optional | optional (a new one replaces the old one) |
| `removeCoverImage` | optional, `true` = no cover image, so none is made from the file ([`auto-fill-plan.md`](auto-fill-plan.md), decision 10) | optional, `true` removes the cover image |

## 6. API

Every request except login carries an `X-User-Email` header. `AllowedEmailFilter` checks that email
against `users` and returns 401 if it is missing or unknown. Errors come back as standard ProblemDetails.

| Request | What it does | Responses |
|---|---|---|
| `GET /api/file-system-items?parentFolderId=` | Folder contents (no parameter = root): folders first, then files | 200 / 404 |
| `GET /api/file-system-items/{itemId}` | One item with everything about it | 200 / 404 |
| `GET /api/file-system-items/{itemId}/path` | Path from the root to the item | 200 / 404 |
| `POST /api/file-system-items` | **Create any item** (the form from section 5) | 200 / 400 / 404 / 409 |
| `PUT /api/file-system-items/{itemId}` | **Edit any item**: name, description, cover image (not the content, decision 46) | 200 / 400 / 404 / 409 |
| `PUT /api/file-system-items/{itemId}/parent-folder` | Move | 200 / 404 / 409 |
| `DELETE /api/file-system-items/{itemId}` | Delete, together with everything inside | 204 / 404 |
| `GET /api/file-system-items/{itemId}/file` | The uploaded file of a photo or document | 200 / 404 |
| `GET /api/file-system-items/{itemId}/cover-image` | The cover image | 200 / 404 |

## 7. Logic (`FileSystemService`)

**Rules** (`FileSystemItemRules`, checked both in the app and on the server):

| What | Rule |
|---|---|
| Type | Required on create; can't change later |
| Content | Required for every type except folders, and must match the type, otherwise 400. Folder: none. Link: `http://` or `https://` URL, up to 2048 characters. Note: text that isn't empty, up to 100,000 characters. Photo: jpg, png, webp or gif. Document: pdf, txt, md, csv or json (text read as UTF-8). Files up to 25 MB. Set on create; editing can't change it (decision 46) |
| Name | Optional, up to 255 characters, surrounding spaces trimmed. When empty: folder → `Нова папка`; link → the site's address (for example `example.com`); note → its first line that isn't empty, cut to 255 characters; photo / document → the original file name. When a photo or document is edited with an empty name, the current name stays |
| Description | Optional, up to 2000 characters |
| Cover image | Optional; jpg, png, webp or gif, up to 5 MB. A new cover image together with `removeCoverImage = true` → 400 «Або нова картинка, або видалення» |
| Name already taken in the folder | 409 |

**Operations:**

| Operation | What the service checks | Errors |
|---|---|---|
| Folder contents | The folder exists and belongs to the current email | 404 |
| Item details | The item belongs to the current email | 404 |
| Create | The rules; the parent is a folder of the current email; the name is free | 400 / 404 / 409 |
| Edit | The rules; the name is free in the same folder | 400 / 404 / 409 |
| Move | The target is a folder of the current email, or the root; not the item itself or a folder inside it; the name is free in the target | 404 / 409 |
| Delete | The item belongs to the current email; the database deletes everything inside it; the service removes that branch's files and cover images from disk | 404 |
| Path | Walks the tree from the item up to the root | 404 |

**Details:**

- **Timestamps.** `PawStashContext` sets `created_at` and `updated_at` on save.
- **Two devices create the same name at once.** The unique index rejects the second request; the service
  returns 409 with a readable message.
- **Simultaneous edits.** The last save wins.
- **Files on disk.** Uploaded files and cover images are stored in `pawstash-api/PawStash.API/data/files/`,
  which is kept out of git. They are named by `item_id` (`{item_id}-file-{random}.ext`,
  `{item_id}-cover-{random}.ext`), so names the user sees can change independently. A replacement is
  written next to the old file, and the old one is removed only after a successful save. Files are removed
  from disk after their rows are deleted.

## 8. App

**Bottom bar (Alina's request, 2026-10-02).** Items are created from a floating bottom bar styled like the
reference: [`docs/images/bottom-bar-reference.png`](images/bottom-bar-reference.png).

- A rounded bar floats above the content, with a bump in the middle.
- In the bump sits a raised round `+` button: accent purple with a soft glow.
- On either side of it are the tabs `Головна` and `Профіль`. The active tab has a small dot under its icon.
- Light and dark variants, following the device setting.

**Creating an item.** `+` opens a small menu above the bar with `Посилання` (first, the main action),
`Нотатка`, `Папка`, `Файл`. The new item goes into the folder that is open at that moment.

- `Файл` opens the system file picker for one file. Its type (photo or document) is taken from the file
  extension; then `ItemPage` opens with the fields already filled ([`auto-fill-plan.md`](auto-fill-plan.md),
  decision 3). Until 2026-10-02 several files could be picked and each was sent as its own create request.
- The other options open `ItemPage` in create mode.

**Using an item (decision 38).**

- **Tapping an item opens it:**
  - folder → goes into the folder;
  - link → the browser;
  - photo, note, text document (txt, md, csv, json) → `ViewerPage` in the app;
  - PDF → the device's viewer. If the device has no PDF viewer, a message says so.
- **Holding an item opens a menu:** `Редагувати`, `Перемістити`, `Видалити`. There is no `⋯` button.

| Page | What it does |
|---|---|
| `FolderPage` (replaces the `HomePage` placeholder) | Path at the top; the list, each row with its cover image or, without one, the type icon; sorting (stage 4); tap / hold as above. An empty folder shows `Тут поки порожньо`. No create buttons here: creating is in the bottom bar |
| `ItemPage` | Only the form to create and edit an item. When creating, a content field by type: URL for a link, text for a note, the picked file with `Замінити файл` for a photo or document, none for a folder. When editing, no content field (decision 46). Plus name, description and a cover image picked from the gallery (it can also be removed). `Зберегти` returns to the folder |
| `ViewerPage` | Opens a photo full screen, or a text (a note or a text document), with the name at the top |
| `MovePage` | Browse folders only, then `Перемістити сюди`. The item itself and the folders inside it can't be picked |
| `ProfilePage` | The signed-in email and `Вийти` |

**Design scope:** only the bottom bar and the creation menu look like the reference now; the pages stay plain until a separate design stage.

**How it is built.** MAUI's own tab bar can't draw a raised centre button with a bump, so:

- the bar is a custom control, `Controls/BottomBar.xaml`, shown at the bottom of every main page;
- the shell's own tab bar is hidden;
- icons are SVG files in `Resources/Images`, from an open-licensed set (Lucide, ISC licence).

**Navigation:** the root is `//home`, then `folder?itemId=`, `item?itemId=` (open/edit),
`item?itemType=&parentFolderId=` (create), `viewer?itemId=`, `move?itemId=`, `//profile`.

**Behaviour:**

- `FileSystemItemRules` checks the data before it is sent.
- Server errors are shown as red text, like on the login page.
- Delete always asks for confirmation.
- On 401 the app signs out and returns to login.

## 9. Stages

Each stage ends with a working version, checked through Swagger and by clicking through the app.

| # | Stage | What appears |
|---|---|---|
| 0 | **Database** | `CreateFileSystemItemsTable`, `AddDescriptionAndCoverImage` |
| 1 | **Server on the general model** | One `FileSystemService` and one `FileSystemItemsController` for everything in section 6, the forms, validators, file and cover image storage |
| 1a | **Android setup** | JDK 17, the `maui-android` workload, the Android SDK, the emulator with a phone image; the app gets an Android target next to Windows; a debug-only setting lets it call the local API over plain HTTP (`10.0.2.2:5094` from the emulator) |
| 2 | **App: bottom bar and folders** | `BottomBar` with `+` and the creation menu, `ApiClient`, `FileSystemApi`, `FolderPage`, `ProfilePage`. Also decision 30 on the server |
| 3 | **App: items** | Tap to open / hold for the menu, `ItemPage` (create and edit), `ViewerPage`, opening links and PDFs |
| 4 | **App: move and sorting** | `MovePage`, list sorting |

**Progress (2026-10-02):** stages 0 and 1 are done. The server was checked over HTTP:

- every create, read, edit, move and delete path;
- default names, including a note's long first line;
- content that doesn't match its type;
- file and cover image limits;
- duplicate names, case-insensitive;
- the rules from decisions 21–24;
- files and cover images served back byte for byte, replaced and removed on disk;
- isolation between emails;
- cascade delete.

Stage 1a is done as well (2026-10-02):

- JDK 17, `maui-android`, the Android SDK and the `PawStash_Phone` emulator (Android 16, hardware-accelerated) are installed per user;
- the app builds for Android and Windows;
- on the emulator the login works against the local API (`10.0.2.2:5094`, plain HTTP allowed in Debug only);
- `run-android.cmd` starts the emulator if needed and runs the app.

Stage 2 is done as well (2026-10-02), checked on the emulator in light and dark themes:

- the bottom bar with `Головна`, `+` and `Профіль`;
- the `+` menu;
- `Файл` through the Android file picker: several files at once, type taken from the extension;
- `FolderPage`: path, cover images, type icons, sizes, nested folders, path jumps, empty folder text;
- delete with confirmation;
- `ProfilePage` with `Вийти`;
- decision 30 on the server.

Stage 3 is done as well (2026-10-02), checked on the emulator, the new pages in light and dark themes:

- tapping opens an item: a folder goes inside, a link opens in Chrome, a photo, note or text document opens in
  `ViewerPage`, a PDF opens in the device's viewer (Google Drive on the emulator);
- without a PDF viewer the message appears (checked by switching Drive off for a moment);
- holding opens `Редагувати`, `Перемістити`, `Видалити`, and a hold doesn't also count as a tap;
- `ItemPage` creates a link, a note and a folder from the `+` menu, in the open folder, with default names;
- `ItemPage` edits name, description, content (`Замінити файл` included) and the cover image: picked from the
  gallery or removed;
- the app's own checks (address, empty note) and server errors (name taken) show as red text;
- after `Зберегти` the app returns to the folder, which shows the change.

Until stage 4 arrives, `Перемістити` shows «Ще не готово».

Stage 4 is next.

## 10. Delivery process

1. **Plan:** agreed in chat.
2. **Plan document:** this file.
3. **Integration:** follows this plan. Any change to a decision is asked first and only then written here.
4. **Fixes** after review.
5. **Manual testing** by Alina.
6. **Automated tests** for this functionality: a `PawStash.Tests` project (xUnit) covering
   `FileSystemService`, run against a throwaway PostgreSQL in Docker.
   - **Where:** `pawstash-api/PawStash.Tests`, xUnit 2 as in SalvageWorks; no mocking library.
   - **Database (decision 47):** each run creates its own database `pawstash_tests_{random}` in the running
     `pawstash-db` container, applies the migrations and drops it at the end. Alina's data isn't touched.
   - **Isolation:** each test signs in as its own new email and stores files in its own temporary folder.
   - **What is real:** the service, the validator, `CoverImageMaker` and `LocalFileStorage`, so the unique name
     index, the `CHECK` constraints, cascade delete and files on disk are tested for real.
   - **Covered:**
     - create and its rules, including default names;
     - edit limited by decision 46;
     - move;
     - delete;
     - folder contents, path and item details;
     - isolation between emails;
     - cover images from [`auto-fill-plan.md`](auto-fill-plan.md);
     - `LinkPageParser`.
   - **Run:** `.\run-tests.cmd`. It starts the database container like `run-api.cmd`, then runs the tests.
   - **Status (2026-10-03):** 100 tests, all passing. Breaking two rules on purpose (EXIF turn, content
     on edit) made exactly their tests fail.

## 11. Decisions

**Confirmed 2026-10-02:**

| # | Question | Decision |
|---|---|---|
| 1 | Deleting a folder that isn't empty | Permanently, after confirmation, together with everything inside |
| 2 | File types | Folder, link (the main type), note, photo, document (PDF and text files) |
| 3 | Data for different emails | Separate per email (`owner_email`) |
| 4 | Automated tests | Yes, after manual testing (section 10, step 6) |
| 5 | Simultaneous edits | The last save wins |
| 6 | Shape of an item | One object: type + content, with optional name, description and cover image. One general endpoint for every type |
| 7 | Links and notes | Through the general endpoint too: their content is the URL or the text |
| 8 | Cover image | Any item can have one, folders included; jpg, png, webp, gif, up to 5 MB |
| 9 | Default names | As in section 7 |
| 10 | Type | Always given explicitly; the server checks the content matches it |
| 11 | Folders | Created through the same endpoint, without content |
| 12 | Rename | Through the general `PUT`; no separate `PUT …/name` |
| 13 | Several files at once | One create request per file. **Replaced on 2026-10-02** by decision 3 of [`auto-fill-plan.md`](auto-fill-plan.md): one file at a time, through `ItemPage` |
| 14 | Description | Up to 2000 characters |
| 15 | Empty note | Not allowed: the content is required |
| 16 | Services | One `FileSystemService` |
| 17 | Migrations | Keep the applied `CreateFileSystemItemsTable`, add `AddDescriptionAndCoverImage` |
| 18 | Swagger | Keep the `Authorize` button for the email header |
| 19 | Configurations | In the `EntityTypeConfigurations/FileSystem/` subfolder |
| 20 | Stages | In the order of section 9 |
| 21 | Name already taken in the folder | 409 |
| 22 | Editing a photo or document with an empty name and no new file | The current name stays |
| 23 | A new cover image together with `removeCoverImage = true` | 400: «Або нова картинка, або видалення» |
| 24 | Default name of a note | Its first line that isn't empty, cut to 255 characters |
| 25 | Creating items in the app | From a `+` in a floating bottom bar, styled like `docs/images/bottom-bar-reference.png` |
| 26 | Tabs next to `+` | `Головна` (the root folder) on the left, `Профіль` (email and `Вийти`) on the right |
| 27 | What `+` opens | A small menu that pops up above the bar with four options |
| 28 | Design now | The bottom bar and the creation menu look like the reference; the pages stay plain until a separate design stage |
| 29 | Theme | Light and dark, following the device setting; purple accent as in the reference |
| 30 | Unknown `itemType` in the form | A Ukrainian message: «Невідомий тип елемента» (done at the start of stage 2) |
| 31 | Stored file names on disk | `{item_id}-file-{random}.ext` and `{item_id}-cover-{random}.ext` |
| 32 | Helper files | Kept and listed in section 4 |
| 33 | Platform | **Mobile only for now** (Alina, 2026-10-02): the app is built and tested as a phone app. Details: section 12 |
| 34 | Mobile platform | Android |
| 35 | Where the app runs while developing | The Android emulator on this PC |
| 36 | The Windows build | Kept, as a quick developer check; real checks happen on Android |
| 37 | Android setup | Its own step before stage 2 (section 9, stage 1a) |
| 38 | Opening and editing | Tapping an item opens it (folder → inside, link → browser, photo / note / text document → `ViewerPage`, PDF → device viewer). Holding an item opens `Редагувати`, `Перемістити`, `Видалити`. No `⋯` button |
| 39 | `ItemPage` | Only the create / edit form |
| 40 | After `Зберегти` | Back to the folder, which shows the change |
| 41 | Cover image source | The system photo gallery |
| 42 | `Профіль` tab | Out of focus for now; it stays as it is |
| 43 | Title bar hidden on the folder and profile pages | Kept for now |
| 44 | Status bar icons follow the theme | Kept |
| 45 | App helper files in section 4 | Not listed |

**Confirmed 2026-10-03:**

| # | Question | Decision |
|---|---|---|
| 46 | What editing can change | Only the name, description and cover image, for every type. The content (a photo's or document's file, a link's address, a note's text) is set on create and can't change. The edit form has no content field, and the edit request has no content fields |
| 47 | Database for automated tests | A throwaway database in the running `pawstash-db` container, created and dropped by each run. No Testcontainers library |

## 12. Open questions

Found during stage 3. The app works as described in "Now" until Alina decides.

| # | Question | Now |
|---|---|---|
| 1 | `ItemPage` titles and labels | Titles `Нове посилання`, `Нова нотатка`, `Нова папка`, `Редагування`; fields `Адреса`, `Текст`, `Файл`, `Назва`, `Опис`, `Обкладинка`; buttons `Вибрати з галереї`, `Прибрати` |
| 2 | Empty name field | Shows the default name in grey (`uk.wikipedia.org`, the note's first line, `Нова папка`) |
| 3 | Description outside the form | Only the edit form shows it; `ViewerPage` shows the content only |
| 4 | Status bar on pages with a title bar (login, `ItemPage`, `ViewerPage`) | Purple, the MAUI template default since stage 1a; on the folder and profile pages it matches the background |
| 5 | Big text documents (up to 25 MB) in `ViewerPage` | Shown in full, so a very big file opens slowly |
| 6 | Leaving `ItemPage` with unsaved changes | Leaves without asking |
