# File system integration plan

**Status:** revised on 2026-10-02 for one general item endpoint (Alina's request). All decisions are
answered (section 11). Stages 0–1 are done. Section 8 was revised for a bottom bar with a `+` button
(Alina's request); its open questions are in section 12. The app (stages 2–4) starts after they are answered.
**Updated:** 2026-10-02

UI strings are quoted exactly as they appear in the app (Ukrainian).

## 1. Goal and scope

A file manager like Windows Explorer: folders nested to any depth. **Every item is one kind of object:**

| Part | Required | Notes |
|---|---|---|
| Type | yes | `folder`, `link`, `note`, `photo`, `document` |
| Content | yes, except for folders | link: a URL; note: text; photo: an image file; document: a PDF or text file |
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

**Out of scope (next steps):** custom fields, tags, search, OCR, design, trash, copying, drag and drop,
multi-select, offline mode, sharing between users, server-side thumbnails, Office formats (docx, xlsx).

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
| `linkUrl` | required for a link | required for a link |
| `noteText` | required for a note | required for a note |
| `file` | required for a photo or document | optional (empty = keep the current file) |
| `coverImage` | optional | optional (a new one replaces the old one) |
| `removeCoverImage` | — | optional, `true` removes the cover image |

## 6. API

Every request except login carries an `X-User-Email` header. `AllowedEmailFilter` checks that email
against `users` and returns 401 if it is missing or unknown. Errors come back as standard ProblemDetails.

| Request | What it does | Responses |
|---|---|---|
| `GET /api/file-system-items?parentFolderId=` | Folder contents (no parameter = root): folders first, then files | 200 / 404 |
| `GET /api/file-system-items/{itemId}` | One item with everything about it | 200 / 404 |
| `GET /api/file-system-items/{itemId}/path` | Path from the root to the item | 200 / 404 |
| `POST /api/file-system-items` | **Create any item** (the form from section 5) | 200 / 400 / 404 / 409 |
| `PUT /api/file-system-items/{itemId}` | **Edit any item**: name, description, content, cover image | 200 / 400 / 404 / 409 |
| `PUT /api/file-system-items/{itemId}/parent-folder` | Move | 200 / 404 / 409 |
| `DELETE /api/file-system-items/{itemId}` | Delete, together with everything inside | 204 / 404 |
| `GET /api/file-system-items/{itemId}/file` | The uploaded file of a photo or document | 200 / 404 |
| `GET /api/file-system-items/{itemId}/cover-image` | The cover image | 200 / 404 |

## 7. Logic (`FileSystemService`)

**Rules** (`FileSystemItemRules`, checked both in the app and on the server):

| What | Rule |
|---|---|
| Type | Required on create; can't change later |
| Content | Required for every type except folders, and must match the type, otherwise 400. Folder: none. Link: `http://` or `https://` URL, up to 2048 characters. Note: text that isn't empty, up to 100,000 characters. Photo: jpg, png, webp or gif. Document: pdf, txt, md, csv or json (text read as UTF-8). Files up to 25 MB |
| Name | Optional, up to 255 characters, surrounding spaces trimmed. When empty: folder → `Нова папка`; link → the site's address (for example `example.com`); note → its first line that isn't empty, cut to 255 characters; photo / document → the original file name. When a photo or document is edited without a new file, the current name stays |
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
  which is kept out of git. They are named by `item_id`, so names the user sees can change independently.
  They are removed from disk after their rows are deleted.

## 8. App

**Bottom bar (Alina's request, 2026-10-02).** Items are created from a floating bottom bar styled like the
reference: [`docs/images/bottom-bar-reference.png`](images/bottom-bar-reference.png).

- A rounded bar floats above the content, with a bump in the middle.
- In the bump sits a raised round `+` button: accent purple with a soft glow.
- On either side of it are tabs (open question 1). The active tab has a small dot under its icon.
- Light and dark variants (open question 4).

**Creating an item.** `+` opens a creation menu (open question 2) with `Посилання` (first, the main action),
`Нотатка`, `Папка`, `Файл`. The new item goes into the folder that is open at that moment.

- `Файл` opens the system file picker. Several files can be picked; each one is sent as its own create
  request. Its type (photo or document) is taken from the file extension and sent explicitly.
- The other options open `ItemPage` in create mode.

| Page | What it does |
|---|---|
| `FolderPage` (replaces the `HomePage` placeholder) | Path at the top; the list, each row with its cover image or, without one, the type icon; sorting. Each row has a `⋯` menu: open, edit, move, delete. An empty folder shows `Тут поки порожньо`. No create buttons here: creating is in the bottom bar |
| `ItemPage` | One page to create, open and edit any item. Content field by type: URL, text, a file picker, or none for folders. Plus name, description, cover image picker, `Зберегти`. Links open in the browser; photos and text show in the app; a PDF opens in the device's viewer |
| `MovePage` | Browse folders only, then `Перемістити сюди`. The item itself and the folders inside it can't be picked |
| `ProfilePage` | The signed-in email and `Вийти` (if the profile tab is kept, open question 1) |

**Design scope:** open question 3.

**How it is built.** MAUI's own tab bar can't draw a raised centre button with a bump, so:

- the bar is a custom control, `Controls/BottomBar.xaml`, shown at the bottom of every main page;
- the shell's own tab bar is hidden;
- icons are SVG files in `Resources/Images`, from an open-licensed set (Lucide, ISC licence).

**Navigation:** the root is `//home`, then `folder?itemId=`, `item?itemId=` (open/edit),
`item?itemType=&parentFolderId=` (create), `move?itemId=`, `//profile`.

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
| 2 | **App: bottom bar and folders** | `BottomBar` with `+` and the creation menu, `ApiClient`, `FileSystemApi`, `FolderPage`, `ProfilePage` (if kept) |
| 3 | **App: items** | `ItemPage`: create, open and edit any type; opening links and files |
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

The app (stages 2–4) is next.

## 10. Delivery process

1. **Plan:** agreed in chat.
2. **Plan document:** this file.
3. **Integration:** follows this plan. Any change to a decision is asked first and only then written here.
4. **Fixes** after review.
5. **Manual testing** by Alina.
6. **Automated tests** for this functionality: a `PawStash.Tests` project (xUnit) covering
   `FileSystemService`, run against a throwaway PostgreSQL in Docker.

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
| 13 | Several files at once | One create request per file |
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

## 12. Open questions

Questions 1–4 are about the bottom bar in section 8; 5–7 are small server gaps found in stage 1. They need Alina's answer before stage 2 starts.

| # | Question | Proposed | Alternatives |
|---|---|---|---|
| 1 | Which tabs sit next to `+`? The reference has four (home, chat, notifications, profile); the app has nothing for chat or notifications | `Головна` (the root folder) on the left, `Профіль` (email and `Вийти`) on the right | Only `Головна`; or keep places for future tabs (`Пошук`, `Теги`) hidden until those features exist |
| 2 | What does `+` open? | A small menu that pops up above the bar with four options | A full-screen sheet with the four options |
| 3 | How much design now? | The bottom bar and the creation menu look like the reference; the pages stay plain until a separate design stage | The whole app in the reference style now |
| 4 | Theme | Light and dark, following the device setting; purple accent as in the reference | Light only for now |
| 5 | An unknown `itemType` in the form (for example `video`) gets ASP.NET's English message «The value 'video' is not valid» | A Ukrainian message: «Невідомий тип елемента» | Leave it |
| 6 | Stored file names on disk are `{item_id}-file-{random}.ext` and `{item_id}-cover-{random}.ext`. The random part lets a replacement be written next to the old file; the old one is removed only after a successful save | Keep | Plain `{item_id}.ext` |
| 7 | Helper files not listed in section 4: `Mappers/FileSystemItemMapper.cs`, `Models/FileUpload.cs`, `Models/FileContent.cs`, `Models/FolderNode.cs`, `Validators/Extensions/RuleBuilderExtensions.cs` | Keep them and list them in section 4 | Fold them into other files |
