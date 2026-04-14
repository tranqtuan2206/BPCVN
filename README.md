# BPCVN — Cộng đồng Bàn Phím Cơ Việt Nam

## Tech Stack
- ASP.NET Core 8 MVC
- Entity Framework Core 8 (Code-First, SQL Server)
- Bootstrap 5.3 (CDN)
- Bootstrap Icons 1.11 (CDN)

## Cấu trúc dự án

```
BPCVN/
├── Controllers/
│   ├── HomeController.cs
│   ├── KitController.cs
│   └── SwitchController.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── DbSeeder.cs
│   └── Migrations/          ← tự sinh sau khi chạy lệnh bên dưới
├── Models/
│   └── Entities/
│       ├── User.cs
│       ├── Kit.cs
│       ├── Switch.cs
│       ├── Keycap.cs
│       ├── Spec.cs
│       └── SoundTest.cs
├── Views/
│   ├── Home/Index.cshtml
│   ├── Kit/Index.cshtml
│   ├── Switch/Index.cshtml
│   └── Shared/_Layout.cshtml
├── wwwroot/
├── appsettings.json
└── Program.cs
```

## Hướng dẫn chạy

### 1. Cấu hình Connection String
Mở `appsettings.json`, sửa `DefaultConnection` cho đúng với SQL Server của bạn:
```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=BPCVNDb;Trusted_Connection=True;TrustServerCertificate=True"
```
Hoặc nếu dùng SQL Server LocalDB:
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BPCVNDb;Trusted_Connection=True"
```

### 2. Cài packages và tạo Migration
```bash
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
```

### 3. Chạy ứng dụng
```bash
dotnet run
```
Mở trình duyệt → `https://localhost:5001`

## Routes hiện có
| URL | Trang |
|-----|-------|
| `/` | Trang chủ |
| `/Kit` | Danh sách Kit |
| `/Switch` | Danh sách Switch |

## Seeder tự động
DbSeeder chạy mỗi khi app khởi động nhưng chỉ insert nếu bảng rỗng.
Dữ liệu mẫu: 5 Kits, 6 Switches, 3 Keycaps.
