# BPCVN — Cộng đồng Bàn Phím Cơ Việt Nam

## Tech Stack
- .NET 8 / ASP.NET Core 8 MVC (Kiến trúc N-Layer)
- Entity Framework Core 8 (Code-First, SQL Server)
- Bootstrap 5.3 & Bootstrap Icons 1.11 (CDN)
- Cloudinary (lưu trữ ảnh Kit và âm thanh trích xuất từ video)
- Xabe.FFmpeg (tách âm từ video và chuyển đổi định dạng âm thanh)

## Cấu trúc dự án

```text
BPCVN/
├── docs/                                # Tài liệu thiết kế (DESIGN.md)
├── tools/                               # Công cụ hỗ trợ (FFmpeg, scripts, graft, skill_UI)
├── src/
│   ├── BPCVN.Domain/                    # Thực thể (Entities), Enums, Constants
│   ├── BPCVN.Application/               # ViewModels, DTOs, Interfaces, Services (Email)
│   ├── BPCVN.Infrastructure/            # AppDbContext, DbSeeder, Migrations, Audio/Image Services
│   └── BPCVN.Web/                       # Project khởi chạy chính (ASP.NET Core MVC)
│       ├── Areas/Admin/                 # Khu vực quản trị (duyệt Pending Queue)
│       ├── Controllers/
│       ├── Views/
│       ├── wwwroot/
│       ├── Properties/launchSettings.json
│       ├── appsettings.json
│       └── Program.cs
├── .github/workflows/deploy.yml         # CI/CD tự động deploy nhánh main
└── BPCVN.sln
```

## Hướng dẫn cài đặt và chạy dự án

### 1. Cấu hình kết nối & Secrets
Mở `src/BPCVN.Web/appsettings.json` (hoặc dùng `appsettings.Development.json` / User Secrets trong thư mục `src/BPCVN.Web`):
- **Chuỗi kết nối SQL Server** (`ConnectionStrings:DefaultConnection`):
  ```json
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=BPCVN;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"
  ```
- **Cloudinary** (`CloudinarySettings:CloudName`, `ApiKey`, `ApiSecret`): cần cấu hình giá trị thật nếu dùng tính năng upload ảnh Kit hoặc upload video SoundTest.
- **SMTP & Admin** (`SmtpSettings`, `AdminSettings:DefaultPassword`): cấu hình gửi email xác thực và mật khẩu Admin mặc định (nếu để placeholder, hệ thống tự sinh mật khẩu ngẫu nhiên và in ra console khi seed lần đầu).

Có thể nạp User Secrets nhanh từ thư mục gốc:
```bash
dotnet user-secrets set "CloudinarySettings:CloudName" "<cloud_name>" --project src/BPCVN.Web
dotnet user-secrets set "CloudinarySettings:ApiKey" "<api_key>" --project src/BPCVN.Web
dotnet user-secrets set "CloudinarySettings:ApiSecret" "<api_secret>" --project src/BPCVN.Web
```

### 2. Build Solution
```bash
dotnet restore BPCVN.sln
dotnet build BPCVN.sln
```

### 3. Migration & Database (tùy chọn)
Khi khởi chạy ứng dụng, `Program.cs` tự động chạy `db.Database.MigrateAsync()` và `DbSeeder.SeedAsync(db)` để tạo bảng và dữ liệu mẫu.
Nếu cần tạo hoặc cập nhật migration thủ công bằng EF Core CLI:
```bash
dotnet ef migrations add <TenMigration> --project src/BPCVN.Infrastructure --startup-project src/BPCVN.Web
dotnet ef database update --project src/BPCVN.Infrastructure --startup-project src/BPCVN.Web
```

### 4. Chạy ứng dụng
Chạy trực tiếp từ thư mục gốc của Solution:
```bash
dotnet run --project src/BPCVN.Web
```
Hoặc dùng chế độ tự tải lại khi sửa code:
```bash
dotnet watch --project src/BPCVN.Web
```
Sau khi chạy, mở trình duyệt tại:
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:5001`

## Các Route chính
| URL | Mô tả |
|-----|-------|
| `/` | Trang chủ & danh sách build mới nhất |
| `/Kit` | Danh sách Kit |
| `/Switch` | Danh sách Switch |
| `/Keycap` | Danh sách Keycap |
| `/Spec/Explore` | Khám phá các bản build |
| `/TypingTest` | Kiểm tra tốc độ gõ phím |
| `/Admin/Pending` | Queue duyệt Master Data (chỉ dành cho role `Admin`) |

## Seeder tự động
`DbSeeder` chạy khi ứng dụng khởi động và chỉ thêm dữ liệu nếu bảng tương ứng đang trống:
- Dữ liệu mẫu: 5 Kits, 6 Switches, 3 Keycaps và tài khoản `Admin` mặc định.
