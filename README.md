TnieYu Package

TnieYu Package là một thư viện tiện ích (Utility Package) toàn diện dành cho Unity, cung cấp các design pattern, công cụ mở rộng Editor, cấu trúc dữ liệu, và các UI Template (hỗ trợ cả UGUI và UI Toolkit) được thiết kế sẵn sàng để tích hợp nhanh chóng vào các dự án game Unity.

📦 Yêu cầu hệ thống (Requirements)

Unity Version: 2021.3 hoặc mới hơn.

Dependencies (Các package phụ thuộc được tự động cài đặt):

UniTask (Xử lý bất đồng bộ mạnh mẽ)

LitMotion & LitMotion.Extensions (Tweening animation hiệu suất cao)

KBCore.Refs (Hỗ trợ assign reference nhanh qua Attribute)

EditorAttributes

Unity.InputSystem

Unity.2D.Tilemap.Extras

🚀 Cài đặt (Installation)

Bạn có thể cài đặt package này thông qua Unity Package Manager (UPM):

Mở Unity Editor.

Đi tới Window -> Package Manager.

Nhấn vào dấu + ở góc trên bên trái và chọn "Add package from git URL...".

Dán đường dẫn Git của repository này vào và nhấn Add.

🛠️ Các tính năng nổi bật (Key Features)

1. Design Patterns (Mẫu thiết kế)

Cung cấp sẵn các lớp cơ sở (base classes) để triển khai các design pattern phổ biến một cách chuẩn xác và an toàn:

Singleton: Singleton<T>, GlobalSingleton<T> (Tự động Don'tDestroyOnLoad), SingletonScriptable<T>.

Registry: Registry<T> & ComponentRegister giúp quản lý, truy xuất tập trung các object/component thay vì dùng FindObjectOfType.

Builder: Giao diện IBuilder<T> chuẩn hóa việc khởi tạo object.

SaveLoad: Giao diện ISaveLoadData<T> và IBinder<T> chuẩn hóa luồng lưu/tải dữ liệu game.

2. SOAP (ScriptableObject Architecture Pattern)

Hệ thống kiến trúc dựa trên ScriptableObject giúp phân tách dữ liệu và logic, giảm thiểu kết dính (decoupling):

Data Variables: SoapData<T>, SoapAbstractData<T>.

Events: SoapEventVoidSo, SoapEventSo<T>.

Trình tạo tự động (Generators): Cung cấp giao diện cửa sổ Editor (Tools/TnieYu/SOAP/...) để tự động sinh code (auto-generate C# scripts) cho các kiểu dữ liệu SOAP tùy chỉnh.

3. Cấu trúc dữ liệu & Tiện ích (Structures & Utilities)

SerializableDictionary<TKey, TValue>: Dictionary có thể serialize và hiển thị trực tiếp trên Unity Inspector. Hỗ trợ cả Abstract Classes.

ObservableValue<T>: Biến chứa dữ liệu có khả năng phát sự kiện (OnValueChanged) ngay khi giá trị thay đổi. Có sẵn PropertyDrawer để hiển thị đẹp trên Inspector.

SerializableGuid: Định danh Guid có thể lưu trữ trong Unity.

Object Pooling: PrefabPoolTracker và PrefabSpawnManager hỗ trợ tạo hệ thống Pool quản lý bộ nhớ mạnh mẽ kết hợp với UniTask.

Actions: TriggerAction và SafeTriggerAction tự động gỡ bỏ callback sau khi kích hoạt thành công (return true).

4. Custom Attributes (Thuộc tính mở rộng)

Làm phong phú thêm trải nghiệm Inspector của Unity:

[AbstractSupport]: Hỗ trợ gán và khởi tạo các class kế thừa (Polymorphism) trên Inspector dùng chung với [SerializeReference].

[AddressableKey]: Hiển thị dropdown để chọn Addressable Key nhanh chóng (lọc theo Type, Label).

[FilePath]: Hỗ trợ kéo thả file và hiển thị đường dẫn lưu trữ, có lọc theo định dạng (ví dụ: .json).

[TnieRequired]: Đánh dấu các trường bắt buộc phải gán dữ liệu (báo đỏ nếu Missing Reference).

5. UI Templates & UI Toolkit

Bộ công cụ hỗ trợ làm UI chuyên nghiệp và nhanh chóng:

UI Toolkit Base: Các base class như BaseElement, CardElement, ContainerElement, FoldoutElement, LayoutElement... với cơ chế tự định nghĩa USS (thông qua Prefix).

Blur Background: Hệ thống quản lý màn hình mờ nền BlurBackgroundManager dễ dàng tích hợp cho Popup/Dialog.

Display UI Base: Lớp SingletonDisplayUI<T> và BehaviourDisplayUI quản lý vòng đời hiển thị của UI.

Screen Text Display: ScreenTextDisplayController dùng ObjectPool và LitMotion để hiển thị Floating Text (Sát thương, thông báo, v.v.).

6. Bộ công cụ Editor (Editor Tools)

Tối ưu hóa workflow trực tiếp trong Unity Editor:

Texture/Sprite Tools (Tools/TnieYu/Texture/):

Advanced Texture Generator: Tạo texture màu/gradient với các hình dạng (bo góc, tròn, vuông) trực tiếp trong Editor.

Quick Setup Texture: Kéo thả hàng loạt ảnh để tự động config Max Size, Pixel Per Unit, Compression.

Easy Sprite Pivot: Đổi Pivot trực quan cho nhiều Sprite cùng lúc.

Quick Slice Multiple: Cắt Sprite sheet tự động theo cột/hàng cho nhiều file cùng lúc.

Sprite Slice Extractor: Trích xuất các lát cắt của Atlas thành các file PNG riêng lẻ.

Sub-Asset Manager: Công cụ kéo thả trong cửa sổ Project để gom/tách sub-asset (ScriptableObject, Animation Clip) nhanh chóng và tự động sửa lỗi mất Reference toàn project.

Inspector Lock (Ctrl + L): Phím tắt bật/tắt chế độ khóa Inspector.

7. Extensions (Phương thức mở rộng)

Rất nhiều hàm tiện ích rút gọn code hằng ngày:

Vector & Transform: With(x, y), RotateTowards2D(), GetRandom(), ConvertPixelToWorld().

Collections: Dictionary.Resize(), List.RemoveIf(), truy vấn IEnumerable nhanh chóng.

VisualElement: CreateChild(), AddClass(), RemoveClass().

Delegate: AddSafe() chống duplicate callback.

String: Parsing an toàn và thao tác chuỗi.

👨‍💻 Tác giả (Author)

Được phát triển bởi TnieYu.

GitHub: tnieyu1706

Cảm ơn bạn đã sử dụng TnieYu Package! Nếu có đóng góp hoặc báo lỗi, vui lòng tạo Issue trên repository.
