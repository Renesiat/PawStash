# File system integration plan

**Status:** decisions confirmed on 2026-10-02 (section 11). Integration in progress: the database layer is
done (section 9).
**Updated:** 2026-10-02

UI strings are quoted exactly as they appear in the app (Ukrainian).

## 1. Goal and scope

A file manager like Windows Explorer: folders nested to any depth, holding four kinds of items:

- **link**: URL + description. Expected to be the most common item, so it comes first everywhere.
- **note**: text written in the app.
- **photo**: jpg, png, webp, gif.
- **document**: PDF and text files (txt, md, csv, json).

Photos and documents are both **uploaded files**. They share storage, columns and endpoints.

**In scope:**

- create, open, rename, edit contents, move, delete;
- a path at the top (`Головна › Рецепти › Супи`), every segment clickable;
- sorting: folders first, then files; by name, modified date or type.

**Out of scope (next steps):** custom fields, tags, search, OCR, design, trash, copying, drag and drop,
multi-select, offline mode, sharing between users, server-side thumbnails, Office formats (docx, xlsx).

## 2. How the file system maps onto the database

- **One table, `file_system_items`,** holds folders and all kinds of files. The type is in the `item_type`
  column. In code these are classes with the common base `FileSystemItem`. EF Core stores them in one
  table (table-per-hierarchy, TPH).

  ```
  FileSystemItem
  ├── Folder
  ├── Link
  ├── Note
  └── UploadedFile
      ├── Photo
      └── Document
  ```

  Why one table:
  - a name is unique within a folder across folders and files together, and one index enforces it;
  - rename, move and delete work the same way for every type;
  - a folder's contents come back in one query.
- **Tree.** Each item knows only the folder it is in (`parent_folder_id`), so a move changes one field.
  Code computes the path and checks that a folder is not being moved into itself: it reads the user's
  folder tree in one query and walks it in memory. One person has hundreds or thousands of rows at most,
  so no complex SQL is needed.
- **No real folders on disk.** The structure lives in the database. Only the contents of uploaded files
  are stored on the server's disk, and the database keeps the file path.

## 3. Database schema

EF migrations create the table from the entity configurations. The SQL below shows the table as it will
look after all stages.

```sql
CREATE TABLE file_system_items
(
    item_id          uuid          NOT NULL,
    owner_email      varchar(254)  NOT NULL,
    parent_folder_id uuid          NULL,
    item_type        varchar(10)   NOT NULL,
    name             varchar(255)  NOT NULL,
    name_lowercase   varchar(255)  GENERATED ALWAYS AS (lower(name)) STORED,
    created_at       timestamptz   NOT NULL,
    updated_at       timestamptz   NOT NULL,
    link_url         varchar(2048) NULL,
    link_description text          NULL,
    note_text        text          NULL,
    file_path        varchar(300)  NULL,
    file_mime_type   varchar(100)  NULL,
    file_size_bytes  bigint        NULL,
    CONSTRAINT pk_file_system_items               PRIMARY KEY (item_id),
    CONSTRAINT fk_file_system_items_owner         FOREIGN KEY (owner_email)      REFERENCES users (email)                ON DELETE CASCADE,
    CONSTRAINT fk_file_system_items_parent_folder FOREIGN KEY (parent_folder_id) REFERENCES file_system_items (item_id) ON DELETE CASCADE,
    CONSTRAINT ck_file_system_items_item_type     CHECK (item_type IN ('folder', 'link', 'note', 'photo', 'document')),
    CONSTRAINT ck_file_system_items_link_url      CHECK (item_type <> 'link' OR link_url IS NOT NULL),
    CONSTRAINT ck_file_system_items_note_text     CHECK (item_type <> 'note' OR note_text IS NOT NULL),
    CONSTRAINT ck_file_system_items_uploaded_file CHECK (item_type NOT IN ('photo', 'document')
                                                         OR (file_path IS NOT NULL AND file_mime_type IS NOT NULL AND file_size_bytes IS NOT NULL))
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
- **`owner_email`.** Each email has its own file system. Sharing between users is out of scope, because
  for now there is one user. It also keeps `test@test.com` apart from real data.
- **Only a folder can be a parent.** The service checks this, because standard database constraints
  can't express it.

**Naming rules:**

- everything lowercase, words separated by `_`;
- primary key named `<entity>_id`;
- columns used by one kind of item start with its name (`link_`, `note_`), and with `file_` for uploaded
  files (photos and documents);
- `item_type` values are lowercase too;
- constraints and indexes are prefixed `pk_`, `fk_`, `ck_`, `uq_`, `ix_`.

**Migration:** one migration, `CreateFileSystemItemsTable` (in `PawStash.DAL/Migrations`), creates the
whole table for every item type. The plan was approved as a whole and nothing is deployed yet, so
splitting the table per stage would only add migrations. The stages after it change code only.

## 4. Where things live

| Layer | Files | Purpose |
|---|---|---|
| `PawStash.Common` | `Enums/FileSystemItemType.cs` | `Folder`, `Link`, `Note`, `Photo`, `Document`. Sent in JSON as a lowercase string (`"folder"`) |
| | `Rules/FileSystemItemRules.cs` | The rules from section 7, shared by the server and the app |
| | `Models/DTO/FileSystem/*.cs` | The DTOs from section 5 |
| `PawStash.DAL` | `Entities/FileSystem/FileSystemItem.cs`, `Folder.cs`, `Link.cs`, `Note.cs`, `UploadedFile.cs`, `Photo.cs`, `Document.cs` | Entities |
| | `EntityTypeConfigurations/FileSystem/FileSystemItemConfiguration.cs` | Table, keys, indexes, `CHECK` constraints, `item_type` values |
| | `EntityTypeConfigurations/FileSystem/LinkConfiguration.cs`, `NoteConfiguration.cs`, `UploadedFileConfiguration.cs` | Column names for each type |
| | `Context/PawStashContext.cs` | `DbSet`, plus `created_at` / `updated_at` set automatically on save |
| `PawStash.BLL` | `Interfaces/ICurrentUser.cs`, `Implementations/CurrentUser.cs` | Email of whoever is making the request |
| | `Interfaces/IFileSystemService.cs`, `Implementations/FileSystemService.cs` | All the logic from section 7 |
| | `Validators/FileSystem/*Validator.cs` | FluentValidation built on `FileSystemItemRules` |
| | `Interfaces/IFileStorage.cs`, `Implementations/LocalFileStorage.cs` | Saving, reading and deleting uploaded files (stage 5) |
| | `Results/ServiceResult.cs` | Plus a variant without data, for delete |
| `PawStash.API` | `Filters/AllowedEmailFilter.cs` | Checks the `X-User-Email` header; 401 without it |
| | `Controllers/FileSystemItemsController.cs` | Folder contents, path, rename, move, delete |
| | `Controllers/FoldersController.cs`, `LinksController.cs`, `NotesController.cs`, `UploadedFilesController.cs` | Creating items and working with the contents of each type |
| `PawStash.App` | `Services/ApiClient.cs` | Adds the email header, parses errors, sends the user back to login on 401 |
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Folder : FileSystemItem { public List<FileSystemItem> Items { get; set; } = []; }
public class Link : FileSystemItem { public string Url { get; set; } = string.Empty; public string? Description { get; set; } }
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

**DTOs** (`PawStash.Common/Models/DTO/FileSystem`). Named the SalvageWorks way: `…Dto`, `…PostDto`, `…PutDto`.

| DTO | Fields | Used for |
|---|---|---|
| `FileSystemItemDto` | `ItemId`, `ParentFolderId`, `ItemType`, `Name`, `UpdatedAt`, `LinkUrl?`, `FileSizeBytes?` | A row in a folder listing |
| `FolderPathItemDto` | `ItemId`, `Name` | One segment of the path at the top |
| `LinkDto` | `ItemId`, `ParentFolderId`, `Name`, `Url`, `Description`, `UpdatedAt` | An opened link |
| `NoteDto` | `ItemId`, `ParentFolderId`, `Name`, `Text`, `UpdatedAt` | An opened note |
| `UploadedFileDto` | `ItemId`, `ParentFolderId`, `ItemType`, `Name`, `MimeType`, `SizeBytes`, `UpdatedAt` | An opened photo or document |
| `FolderPostDto` | `ParentFolderId`, `Name` | Create a folder |
| `LinkPostDto` / `LinkPutDto` | `ParentFolderId`, `Name`, `Url`, `Description` / same without `ParentFolderId` | Create / edit a link |
| `NotePostDto` / `NotePutDto` | `ParentFolderId`, `Name`, `Text` / `Name`, `Text` | Create / edit a note |
| `ItemNamePutDto` | `Name` | Rename any item |
| `ItemParentFolderPutDto` | `TargetFolderId` (`null` = root) | Move |

## 6. API

Every request except login carries an `X-User-Email` header. `AllowedEmailFilter` checks that email
against `users` and returns 401 if it is missing or unknown. Errors come back as standard ProblemDetails,
like login does now.

| Request | What it does | Responses |
|---|---|---|
| `GET /api/file-system-items?parentFolderId=` | Folder contents (no parameter = root): folders first, then files | 200 / 404 |
| `GET /api/file-system-items/{itemId}/path` | Path from the root to the item | 200 / 404 |
| `PUT /api/file-system-items/{itemId}/name` | Rename | 200 / 400 / 404 / 409 |
| `PUT /api/file-system-items/{itemId}/parent-folder` | Move | 200 / 404 / 409 |
| `DELETE /api/file-system-items/{itemId}` | Delete, together with everything inside | 204 / 404 |
| `POST /api/folders` | Create a folder | 200 / 400 / 404 / 409 |
| `POST /api/links` · `GET` · `PUT /api/links/{itemId}` | Create, open, edit a link | 200 / 400 / 404 / 409 |
| `POST /api/notes` · `GET` · `PUT /api/notes/{itemId}` | The same for notes | 200 / 400 / 404 / 409 |
| `POST /api/uploaded-files` | Upload one or more photos or documents (`multipart/form-data`). The type is picked by extension | 200 / 400 / 404 / 409 |
| `GET /api/uploaded-files/{itemId}` · `/api/uploaded-files/{itemId}/content` | File details / the file itself | 200 / 404 |

## 7. Logic (`FileSystemService`)

**Rules** (`FileSystemItemRules`, checked both in the app and on the server):

| What | Rule |
|---|---|
| Name | Not empty, up to 255 characters, surrounding spaces trimmed |
| New folder | `Нова папка` is suggested; if it is taken, `Нова папка (2)` and so on |
| Link URL | `http://` or `https://`, up to 2048 characters |
| Note text | Up to 100,000 characters |
| Photo | jpg, png, webp or gif |
| Document | pdf, txt, md, csv or json. Text files are read as UTF-8 |
| Upload size | Up to 25 MB per file, up to 100 MB per upload request |
| Uploaded file name | The original file name is used by default and can be renamed later |

**Operations:**

| Operation | What the service checks | Errors |
|---|---|---|
| Folder contents | The folder exists and belongs to the current email | 404 |
| Create (folder, link, note, uploaded file) | The rules; the parent is a folder of the current email; the name is free | 400 / 404 / 409 |
| Rename | The rules; the name is free in the same folder | 400 / 404 / 409 |
| Edit a link or note | The content rules | 400 / 404 |
| Move | The target is a folder of the current email, or the root; not the item itself or a folder inside it; the name is free in the target | 404 / 409 |
| Delete | The item belongs to the current email; the database deletes everything inside it; the service removes that branch's uploaded files from disk | 404 |
| Path | Walks the tree from the item up to the root | 404 |

**Details:**

- **Timestamps.** `PawStashContext` sets `created_at` and `updated_at` on save.
- **Two devices create the same name at once.** The unique index rejects the second request. The service
  catches the database error and returns 409 with a readable message.
- **Simultaneous edits.** The last save wins.
- **Uploaded files** are stored in `pawstash-api/PawStash.API/data/files/`, which is kept out of git. The
  file is named by `item_id`, so the name the user sees can change independently. Files are removed from
  disk after their rows are deleted from the database.

## 8. App (no design yet)

| Page | What it does |
|---|---|
| `FolderPage` (replaces the `HomePage` placeholder) | Path at the top, the list, `+ Посилання` (first, the main action) / `+ Нотатка` / `+ Папка` / `+ Файл` buttons, sorting, `Вийти`. Each row has a `⋯` menu: open, rename, move, delete. An empty folder shows `Тут поки порожньо` |
| `LinkPage` | Name, URL, description, `Відкрити в браузері`, `Зберегти` |
| `NotePage` | Name, text, `Зберегти` |
| `UploadedFilePage` | A photo is shown as an image, a text document as read-only text. A PDF opens in the device's default viewer via `Відкрити` |
| `MovePage` | Browse folders only, then `Перемістити сюди`. The item itself and the folders inside it can't be picked |

`+ Файл` opens the system file picker, filtered to the allowed photo and document formats. Several files
can be picked at once.

**Navigation:** the root is `//home`, then `folder?itemId=`, `link?itemId=` (a new link:
`link?parentFolderId=`), `note?…`, `file?itemId=`, `move?itemId=`.

**Behaviour:**

- `FileSystemItemRules` checks the data before it is sent.
- Server errors are shown as red text, like on the login page.
- Delete always asks for confirmation.
- On 401 the app signs out and returns to login.

## 9. Stages

Each stage ends with a working version, checked through Swagger and by clicking through the app.

| # | Stage | What appears |
|---|---|---|
| 0 | **Database layer** (done 2026-10-02) | DAL: all entities (`FileSystemItem`, `Folder`, `Link`, `Note`, `UploadedFile`, `Photo`, `Document`), their configurations, `CreateFileSystemItemsTable`, automatic timestamps in `PawStashContext`. Common: length limits in `FileSystemItemRules` |
| 1 | **Request protection and folders** | Common: `FileSystemItemType`, name validation, folder DTOs. BLL: `CurrentUser`, `FileSystemService` (contents, create folder, rename, delete, path), validators. API: `AllowedEmailFilter`, `FileSystemItemsController`, `FoldersController`. App: `ApiClient`, `FileSystemApi`, `FolderPage` |
| 2 | **Links** | Link operations in `FileSystemService`, `LinksController`, `LinkPage`, opening in the browser |
| 3 | **Notes** | Note operations, `NotesController`, `NotePage` |
| 4 | **Move and sorting** | Cycle check, `PUT …/parent-folder`, `MovePage`, list sorting |
| 5 | **Uploaded files** | `LocalFileStorage`, upload of several files, `UploadedFilesController`, `UploadedFilePage`, deleting files from disk |

Stage 0 was checked directly against PostgreSQL and through EF Core:

- every `CHECK`, foreign key and the unique index reject bad rows, including case-insensitive duplicates
  at the root;
- cascade delete works for both folders and users;
- each type is stored with its lowercase `item_type` and loads back as the right class;
- timestamps and `name_lowercase` are filled in on insert and on rename.

## 10. Delivery process

1. **Plan:** agreed in chat.
2. **Plan document:** this file.
3. **Integration:** the database layer (done), then stages 1–5.
4. **Fixes** after review.
5. **Manual testing** by Alina.
6. **Automated tests** for this functionality: a `PawStash.Tests` project (xUnit) covering
   `FileSystemService`, run against a throwaway PostgreSQL in Docker. They cover:
   - name rules, including case-insensitive duplicates;
   - cycle checks on move;
   - cascade delete together with file cleanup;
   - isolation between emails.

## 11. Decisions (confirmed 2026-10-02)

| # | Question | Decision |
|---|---|---|
| 1 | Deleting a folder that isn't empty | Permanently, after confirmation, together with everything inside (database cascade) |
| 2 | File types | Folder, link (the main type), note, photo, document (PDF and text files) |
| 3 | Data for different emails | Separate per email (`owner_email`). Sharing between users is not planned while there is one user |
| 4 | Automated tests | Yes, after manual testing (section 10, step 6) |
| 5 | Simultaneous edits | The last save wins |
