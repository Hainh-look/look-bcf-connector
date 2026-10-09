# CODEX TECHNICAL SPECIFICATION: LOOK BCF NATIVE MANAGER FOR REVIT

> **Mục tiêu:** Chuyển đổi Add-in Look BCF từ mô hình nhúng trình duyệt (WebView2 loading full OpenProject portal) sang **Native BCF Client (chuẩn BIMcollab BCF Manager)** chạy trên Autodesk Revit 2021–2026.

---

## 1. TỔNG QUAN VÀ BỐI CẢNH (OVERVIEW & CONTEXT)

### 1.1 Vấn đề của phiên bản cũ
- **Cơ chế cũ:** Add-in nhúng điều khiển trình duyệt (WebView2) và tải toàn bộ trang web `https://bim.lookbim.com` vào Dockable Panel của Revit.
- **Hạn chế:**
  - Panel trong Revit chỉ rộng ~350px–450px nhưng phải chứa toàn bộ header, sidebar, footer của OpenProject web gây chật chội.
  - Phụ thuộc vào việc render trang web từ máy chủ, gây cảm giác giật/lag khi kết nối qua VM/mạng chậm.
  - Người dùng không có cảm giác đang dùng một công cụ Revit chuyên dụng như BIMcollab hay Revizto.

### 1.2 Giải pháp mới (Kiến trúc Native BCF Manager)
- **Cơ chế mới:** Xây dựng giao diện Palette thuần **WPF (XAML)** trong C#.
- **Giao tiếp đám mây:** Kết nối trực tiếp với OpenProject qua chuẩn công nghiệp quốc tế **buildingSMART BCF REST API 2.1**.
- **Môi trường hoạt động:** 
  - Revit 2021–2024: Target `.NET Framework 4.8` (`net48`).
  - Revit 2025–2026: Target `.NET 8.0 Windows` (`net8.0-windows`).

```
┌──────────────────────────────────────────────────────────────────┐
│                   AUTODESK REVIT DOCKABLE PANEL                  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ WPF UI (LookBcfPanel.xaml + MVVM ViewModels)               │  │
│  │ - Dropdown Dự án                                           │  │
│  │ - Thẻ Issue Cards (Thumbnail, Badge trạng thái, Người làm) │  │
│  │ - Nút [🎯 Xem trong Revit] & [📷 Tạo Issue từ Revit]       │  │
│  └───────────────▲───────────────────────────▲────────────────┘  │
│                  │                           │                   │
│   (Revit Thread / ExternalEvent)    (REST HTTP Async / JSON)     │
│                  │                           │                   │
│  ┌───────────────▼──────────────┐   ┌────────▼────────────────┐  │
│  │ REVIT API INTEGRATION        │   │ BCF API CLIENT (C#)     │  │
│  │ - OpenViewpointEventHandler  │   │ - BcfApiClient.cs       │  │
│  │ - CreateViewpointEventHandler│   │ - HttpClient Basic Auth │  │
│  │ - 3D Camera / Section Box    │   │                         │  │
│  └──────────────────────────────┘   └────────┬────────────────┘  │
└──────────────────────────────────────────────┼───────────────────┘
                                               │ HTTPS
                                               ▼
                              ┌─────────────────────────────────┐
                              │ OPENPROJECT SERVER (LOOK BCF)   │
                              │ https://bim.lookbim.com         │
                              │ /api/bcf/2.1/...                │
                              └─────────────────────────────────┘
```

---

## 2. TÀI NGUYÊN ĐÃ CÓ TRONG CODEBASE (TÁI SỬ DỤNG 100%)

Codex **không cần** tự viết lại các thuật toán hình học và xử lý luồng Revit. Toàn bộ logic cốt lõi đã có sẵn:

1. **Mô hình dữ liệu chuẩn BCF (`iabi.BCF`):**
   - Đã được tích hợp trong project `OpenProject.Shared`.
   - Cung cấp sẵn các DTO chuẩn BCF 2.1: `Topic_GET`, `Topic_POST`, `Viewpoint_GET`, `Viewpoint_POST`, `Comment_GET`, `Comment_POST`, `VisualizationInfo`.

2. **Xoay Camera 3D & Highlight cấu kiện (`OpenViewpointEventHandler.cs`):**
   - Đã có phương thức tĩnh:
     ```csharp
     OpenViewpointEventHandler.ShowBcfViewpoint(BcfViewpointWrapper bcfViewpoint);
     ```
   - Cơ chế: Tự động kích hoạt `ExternalEvent.Raise()`, chuyển góc nhìn 3D của Revit theo tọa độ vector `(CameraViewPoint, CameraDirection, CameraUpVector)`, áp dụng mặt cắt Section Box và bôi đỏ/chọn các cấu kiện theo `IfcGuid` hoặc `UniqueId`.

3. **Chụp ảnh & Bắt góc nhìn 3D hiện tại (`CreateViewpointEventHandler.cs`):**
   - Đã có phương thức tĩnh:
     ```csharp
     CreateViewpointEventHandler.CaptureAndSendViewpoint(string trackingId, Action<string, string> callback);
     ```
   - Logic bên dưới sử dụng `uiDocument.GenerateViewpoint()` trong `RevitDocumentExtensions.cs` để trích xuất ma trận camera, lấy ID các cấu kiện người dùng đang chọn và chụp ảnh active 3D view thành mảng byte PNG.

---

## 3. ĐẶC TẢ BCF REST API 2.1 TRÊN OPENPROJECT

Base URL: `https://bim.lookbim.com/api/bcf/2.1/`

### 3.1 Xác thực (Authentication)
- Header HTTP: `Authorization: Basic <base64-credentials>`
  - Dạng 1: `username:password` (Tài khoản người dùng OpenProject).
  - Dạng 2: `apikey:<api_token>` (Token tạo tại *My Account -> Access tokens -> API tokens*).
- Kiểm tra tính hợp lệ: Gọi `GET /api/bcf/2.1/projects`.
  - Nếu mã `401 Unauthorized`: Sai thông tin đăng nhập.
  - Nếu mã `200 OK`: Xác thực thành công.

### 3.2 Bảng chi tiết Endpoints
| Thao tác | HTTP Method & URL | Dữ liệu gửi (Request) | Dữ liệu nhận (Response) |
| :--- | :--- | :--- | :--- |
| **Lấy danh sách Dự án** | `GET /api/bcf/2.1/projects` | Không | `List<Project_GET>`: mỗi item gồm `project_id`, `name`. |
| **Lấy danh sách Topics** | `GET /api/bcf/2.1/projects/{projectId}/topics` | Không | `List<Topic_GET>`: chứa `guid`, `title`, `topic_status`, `topic_type`, `priority`, `assigned_to`, `creation_date`,... |
| **Tạo Topic mới** | `POST /api/bcf/2.1/projects/{projectId}/topics` | JSON `Topic_POST` | `Topic_GET` (kèm `guid` vừa tạo). |
| **Lấy danh sách Viewpoints** | `GET /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/viewpoints` | Không | `List<Viewpoint_GET>`: mỗi item có `guid`. |
| **Lấy chi tiết Camera Viewpoint** | `GET /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/viewpoints/{vpGuid}` | Không | JSON Viewpoint chi tiết $\rightarrow$ Parse thành `BcfViewpointWrapper`. |
| **Tải ảnh Snapshot PNG** | `GET /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/viewpoints/{vpGuid}/snapshot` | Không | Binary `byte[]` ảnh PNG. |
| **Thêm Viewpoint vào Topic** | `POST /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/viewpoints` | JSON `Viewpoint_POST` (kèm snapshot base64) | `Viewpoint_GET`. |
| **Lấy Comments** | `GET /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/comments` | Không | `List<Comment_GET>`. |
| **Thêm Comment** | `POST /api/bcf/2.1/projects/{projectId}/topics/{topicGuid}/comments` | JSON `Comment_POST` | `Comment_GET`. |

---

## 4. CHI TIẾT CÁC MODULE CODEX CẦN XÂY DỰNG

### 4.1 Module 1: `BcfApiClient.cs`
Tạo tại `src/OpenProject.Shared/BcfApi/BcfApiClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using iabi.BCF.APIObjects.V21;
using Newtonsoft.Json;
using OpenProject.Shared.BcfApi;

namespace OpenProject.Shared.BcfApi
{
  public class BcfApiClient
  {
    private readonly HttpClient _client;
    private string _baseUrl;

    public BcfApiClient()
    {
      var handler = new HttpClientHandler
      {
        ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
      };
      _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Configure(string serverUrl, string username, string passwordOrToken)
    {
      _baseUrl = serverUrl.TrimEnd('/') + "/api/bcf/2.1";
      var rawCreds = $"{username}:{passwordOrToken}";
      var base64Creds = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCreds));
      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Creds);
      _client.DefaultRequestHeaders.Accept.Clear();
      _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<Project_GET>> GetProjectsAsync()
    {
      var resp = await _client.GetAsync($"{_baseUrl}/projects");
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<List<Project_GET>>(json);
    }

    public async Task<List<Topic_GET>> GetTopicsAsync(string projectId)
    {
      var resp = await _client.GetAsync($"{_baseUrl}/projects/{projectId}/topics");
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<List<Topic_GET>>(json);
    }

    public async Task<Topic_GET> CreateTopicAsync(string projectId, Topic_POST topic)
    {
      var content = new StringContent(JsonConvert.SerializeObject(topic), Encoding.UTF8, "application/json");
      var resp = await _client.PostAsync($"{_baseUrl}/projects/{projectId}/topics", content);
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<Topic_GET>(json);
    }

    public async Task<List<Viewpoint_GET>> GetViewpointsAsync(string projectId, string topicGuid)
    {
      var resp = await _client.GetAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/viewpoints");
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<List<Viewpoint_GET>>(json);
    }

    public async Task<byte[]> GetSnapshotBytesAsync(string projectId, string topicGuid, string viewpointGuid)
    {
      return await _client.GetByteArrayAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/viewpoints/{viewpointGuid}/snapshot");
    }

    public async Task<BcfViewpointWrapper> GetViewpointWrapperAsync(string projectId, string topicGuid, string viewpointGuid)
    {
      var resp = await _client.GetAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/viewpoints/{viewpointGuid}");
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      var dummyEvent = new WebUiMessageEventArgs(MessageTypes.VIEWPOINT_DATA, viewpointGuid, json);
      return MessageDeserializer.DeserializeBcfViewpoint(dummyEvent);
    }

    public async Task<Viewpoint_GET> AddViewpointAsync(string projectId, string topicGuid, Viewpoint_POST viewpoint)
    {
      var content = new StringContent(JsonConvert.SerializeObject(viewpoint), Encoding.UTF8, "application/json");
      var resp = await _client.PostAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/viewpoints", content);
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<Viewpoint_GET>(json);
    }

    public async Task<List<Comment_GET>> GetCommentsAsync(string projectId, string topicGuid)
    {
      var resp = await _client.GetAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/comments");
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<List<Comment_GET>>(json);
    }

    public async Task<Comment_GET> AddCommentAsync(string projectId, string topicGuid, Comment_POST comment)
    {
      var content = new StringContent(JsonConvert.SerializeObject(comment), Encoding.UTF8, "application/json");
      var resp = await _client.PostAsync($"{_baseUrl}/projects/{projectId}/topics/{topicGuid}/comments", content);
      resp.EnsureSuccessStatusCode();
      var json = await resp.Content.ReadAsStringAsync();
      return JsonConvert.DeserializeObject<Comment_GET>(json);
    }
  }
}
```

---

### 4.2 Module 2: Lưu trữ Thông tin Đăng nhập (`CredentialStore.cs`)
Tạo tại `src/OpenProject.Shared/BcfApi/CredentialStore.cs`:
- Lưu trữ cấu hình `ServerUrl`, `Username`, `PasswordOrToken` vào file cục bộ `%LOCALAPPDATA%\LookBcf\settings.json`.
- Sử dụng `ProtectedData.Protect` (Windows DPAPI) nếu muốn tăng cường bảo mật mật khẩu.

---

### 4.3 Module 3: ViewModel Layer (MVVM)
Xây dựng trong `src/OpenProject.Revit/UI/ViewModels/`:

1. **`LookBcfMainViewModel.cs`**:
   - `ObservableCollection<Project_GET> Projects { get; set; }`
   - `Project_GET SelectedProject { get; set; }` (Tự động tải lại Topics khi đổi).
   - `ObservableCollection<TopicItemViewModel> Topics { get; set; }`
   - `ObservableCollection<TopicItemViewModel> FilteredTopics { get; set; }`
   - `TopicItemViewModel SelectedTopic { get; set; }`
   - `string SearchText { get; set; }`
   - `string SelectedStatusFilter { get; set; }` (All, Open, In Progress, Closed)
   - `bool IsBusy { get; set; }`
   - Các lệnh (ICommand):
     - `RefreshCommand`: Tải lại danh sách Topics.
     - `ShowInRevitCommand`: Nhảy camera Revit đến viewpoint của `SelectedTopic`.
     - `CreateIssueFromRevitCommand`: Chụp ảnh 3D Revit $\rightarrow$ mở hộp thoại tạo Issue.

2. **`TopicItemViewModel.cs`**:
   - Wrap đối tượng `Topic_GET`.
   - `BitmapImage ThumbnailImage { get; set; }` (Tải bất đồng bộ từ API `GetSnapshotBytesAsync`).
   - `string StatusColorBrush { get; }` (Ví dụ: Open $\rightarrow$ Đỏ `#E02424`, In Progress $\rightarrow$ Vàng `#E3A008`, Closed $\rightarrow$ Xanh `#0E9F6E`).

---

### 4.4 Module 4: Giao diện XAML (`LookBcfPanel.xaml`)
Thay thế thẻ `WebView2` cũ bằng cấu trúc Native WPF gọn gàng, chia theo lưới:

```xml
<UserControl x:Class="OpenProject.Revit.UI.LookBcfPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
             mc:Ignorable="d" 
             d:DesignHeight="700" d:DesignWidth="380"
             Background="#F8FAFC">
  <Grid>
    <Grid.RowDefinitions>
      <!-- Row 0: Header & Server status -->
      <RowDefinition Height="Auto"/>
      <!-- Row 1: Project Selector -->
      <RowDefinition Height="Auto"/>
      <!-- Row 2: Search & Filter Bar -->
      <RowDefinition Height="Auto"/>
      <!-- Row 3: Topics List (Cards) -->
      <RowDefinition Height="*"/>
      <!-- Row 4: Detail & Actions (Zoom to Revit) -->
      <RowDefinition Height="Auto"/>
      <!-- Row 5: Bottom Command Button (Create from Revit) -->
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Header -->
    <Border Grid.Row="0" Background="#05002C" Padding="12,8">
      <DockPanel>
        <StackPanel Orientation="Horizontal" DockPanel.Dock="Left">
          <TextBlock Text="LOOK BCF" FontWeight="Bold" Foreground="White" FontSize="14" VerticalAlignment="Center"/>
          <Border Margin="8,0,0,0" CornerRadius="4" Background="#1F883D" Padding="6,2" VerticalAlignment="Center">
            <TextBlock Text="ONLINE" Foreground="White" FontSize="10" FontWeight="SemiBold"/>
          </Border>
        </StackPanel>
        <Button DockPanel.Dock="Right" Content="⚙ Cài đặt" Background="Transparent" BorderThickness="0" Foreground="#94A3B8"
                Click="OnSettingsClicked" HorizontalAlignment="Right"/>
      </DockPanel>
    </Border>

    <!-- Project Selector -->
    <Border Grid.Row="1" Background="White" BorderBrush="#E2E8F0" BorderThickness="0,0,0,1" Padding="10,8">
      <StackPanel Orientation="Horizontal">
        <TextBlock Text="Dự án:" VerticalAlignment="Center" FontWeight="SemiBold" Foreground="#334155" Margin="0,0,8,0"/>
        <ComboBox ItemsSource="{Binding Projects}" DisplayMemberPath="Name" 
                  SelectedItem="{Binding SelectedProject}" MinWidth="260" Height="28"/>
      </StackPanel>
    </Border>

    <!-- Search & Filter Bar -->
    <Grid Grid.Row="2" Margin="10,8">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <TextBox Grid.Column="0" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" 
               Height="28" VerticalContentAlignment="Center" Padding="4,0" 
               d:Text="Tìm kiếm issue..."/>
      <ComboBox Grid.Column="1" Margin="6,0,0,0" Width="100" Height="28" 
                SelectedItem="{Binding SelectedStatusFilter}">
        <ComboBoxItem Content="Tất cả"/>
        <ComboBoxItem Content="Open"/>
        <ComboBoxItem Content="In Progress"/>
        <ComboBoxItem Content="Closed"/>
      </ComboBox>
      <Button Grid.Column="2" Margin="6,0,0,0" Width="30" Height="28" Content="🔄" 
              Command="{Binding RefreshCommand}" ToolTip="Làm mới danh sách"/>
    </Grid>

    <!-- Topics Cards List -->
    <ListBox Grid.Row="3" ItemsSource="{Binding FilteredTopics}" SelectedItem="{Binding SelectedTopic}"
             ScrollViewer.HorizontalScrollBarVisibility="Disabled" BorderThickness="0" Background="Transparent"
             Margin="6,0">
      <ListBox.ItemTemplate>
        <DataTemplate>
          <Border Margin="0,4" Background="White" CornerRadius="6" BorderBrush="#E2E8F0" BorderThickness="1" Padding="8">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="70"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <!-- Snapshot Thumbnail -->
              <Border Grid.Column="0" Width="64" Height="48" Background="#E2E8F0" CornerRadius="4" ClipToBounds="True">
                <Image Source="{Binding ThumbnailImage}" Stretch="UniformToFill"/>
              </Border>
              <!-- Issue Info -->
              <StackPanel Grid.Column="1" Margin="8,0,0,0">
                <TextBlock Text="{Binding Title}" FontWeight="SemiBold" FontSize="13" Foreground="#0F172A" TextTrimming="CharacterEllipsis"/>
                <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                  <Border Background="{Binding StatusBgColor}" CornerRadius="3" Padding="4,1" Margin="0,0,6,0">
                    <TextBlock Text="{Binding StatusText}" FontSize="10" Foreground="White" FontWeight="Bold"/>
                  </Border>
                  <TextBlock Text="{Binding Priority}" FontSize="11" Foreground="#64748B" VerticalAlignment="Center"/>
                </StackPanel>
                <TextBlock Text="{Binding AssignedTo}" FontSize="11" Foreground="#94A3B8" Margin="0,2,0,0" TextTrimming="CharacterEllipsis"/>
              </StackPanel>
            </Grid>
          </Border>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>

    <!-- Action Details Panel (Visible when an issue is selected) -->
    <Border Grid.Row="4" Background="White" BorderBrush="#E2E8F0" BorderThickness="0,1,0,0" Padding="12,10"
            Visibility="{Binding HasSelectedTopic, Converter={StaticResource BoolToVisConverter}}">
      <StackPanel>
        <Button Content="🎯 XEM GÓC NHÌN TRONG REVIT" Height="36" FontWeight="Bold" FontSize="12"
                Background="#1F883D" Foreground="White" BorderThickness="0"
                Command="{Binding ShowInRevitCommand}"/>
        <Button Content="💬 Thêm bình luận" Height="28" Margin="0,6,0,0"
                Background="#F1F5F9" Foreground="#334155" BorderBrush="#CBD5E1"
                Command="{Binding AddCommentCommand}"/>
      </StackPanel>
    </Border>

    <!-- Bottom Global Action Button -->
    <Border Grid.Row="5" Background="#F8FAFC" Padding="10,8" BorderBrush="#E2E8F0" BorderThickness="0,1,0,0">
      <Button Content="📷 + TẠO ISSUE MỚI TỪ REVIT" Height="38" FontWeight="Bold" FontSize="13"
              Background="#275BB5" Foreground="White" BorderThickness="0"
              Command="{Binding CreateIssueFromRevitCommand}"/>
    </Border>

    <!-- Loading Overlay -->
    <Grid Background="#80000000" Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisConverter}}">
      <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
        <ProgressBar IsIndeterminate="True" Width="140" Height="10"/>
        <TextBlock Text="Đang xử lý BCF..." Foreground="White" Margin="0,8,0,0" HorizontalAlignment="Center"/>
      </StackPanel>
    </Grid>
  </Grid>
</UserControl>
```

---

## 5. ĐIỀU PHỐI ĐA LUỒNG REVIT (CRITICAL THREAD SAFETY RULES)

Revit API là môi trường đơn luồng (Single-Threaded Apartment). Việc gọi hàm Revit ngoài UI thread sẽ làm crash ứng dụng ngay lập tức:

### 5.1 Xử lý Nút `[🎯 XEM TRONG REVIT]`
```csharp
private async void OnShowInRevitClicked()
{
    if (SelectedTopic == null) return;
    IsBusy = true;
    try
    {
        // 1. Chạy trên Background Thread: Gọi API tải chi tiết Viewpoint
        var viewpoints = await _apiClient.GetViewpointsAsync(SelectedProject.ProjectId, SelectedTopic.Guid);
        if (viewpoints.Count == 0) return;

        var viewpointWrapper = await _apiClient.GetViewpointWrapperAsync(
            SelectedProject.ProjectId, SelectedTopic.Guid, viewpoints[0].Guid);

        // 2. Chuyển quyền điều khiển cho Revit Main Thread: Kích hoạt ExternalEvent
        OpenViewpointEventHandler.ShowBcfViewpoint(viewpointWrapper);
    }
    finally
    {
        IsBusy = false;
    }
}
```

### 5.2 Xử lý Nút `[📷 + TẠO ISSUE MỚI TỪ REVIT]`
```csharp
private void OnCreateIssueFromRevitClicked()
{
    // 1. Gọi ExternalEvent của Revit để chụp ảnh active view và đọc camera vector
    CreateViewpointEventHandler.CaptureAndSendViewpoint("new_issue", (trackingId, viewpointJson) =>
    {
        // 2. Revit hoàn tất chụp ảnh -> Callback được gọi -> Quay về UI Thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            var createDialog = new CreateTopicWindow(viewpointJson, SelectedProject);
            if (createDialog.ShowDialog() == true)
            {
                // Refresh lại danh sách Topic
                RefreshCommand.Execute(null);
            }
        });
    });
}
```

---

## 6. DANH SÁCH CÔNG VIỆC THEO TRÌNH TỰ (ACTION CHECKLIST CHO CODEX)

1. [ ] **Tạo Service:** Thêm file `BcfApiClient.cs` vào `src/OpenProject.Shared/BcfApi/`.
2. [ ] **Tạo Store:** Thêm `CredentialStore.cs` để lưu URL `https://bim.lookbim.com` và credentials.
3. [ ] **Tạo ViewModels:** Thêm `LookBcfMainViewModel.cs` và `TopicItemViewModel.cs` trong `src/OpenProject.Revit/UI/ViewModels/`.
4. [ ] **Thiết kế UI:** Thay thế WebView2 trong `LookBcfPanel.xaml` và `LookBcfPanel.xaml.cs` bằng Native XAML.
5. [ ] **Tạo Dialog Tạo Issue:** Thêm `CreateTopicWindow.xaml` (Gồm trường Nhập Title, Type, Status, xem trước Snapshot và nút Lưu).
6. [ ] **Đấu nối ExternalEvent:** Kết nối `ShowBcfViewpoint` và `CaptureAndSendViewpoint` vào ViewModel commands.
7. [ ] **Kiểm tra Biên dịch:** Chạy `dotnet build -c Release` xác nhận thành công cho cả `net48` và `net8.0-windows`.
8. [ ] **Test thực tế:** Chạy lệnh `./deploy-look-bcf.ps1 -RevitVersion 2024` và mở Revit 2024 kiểm tra chức năng.
