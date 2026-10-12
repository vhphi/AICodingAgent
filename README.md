# AICodingAgent

AI Software Engineering Agent cho workflow:

1. Đọc Requirement từ file hoặc URL.
2. Phân tích requirement.
3. Phát hiện ambiguity.
4. Tìm context trong source code.
5. Lập implementation plan.
6. Generate/modify code.
7. Build.
8. Generate unit test.
9. Run test.
10. Tự sửa một lần khi build/test fail.
11. Review diff theo requirement.
12. Có thể tạo feature branch và commit.

## 1. Kiến trúc

```text
VS Code / CLI
      |
      v
ASP.NET Core API
      |
      v
Application Workflow
      |
      +-- RequirementAgent
      +-- PlanningAgent
      +-- CodingAgent
      +-- TestingAgent
      +-- ReviewAgent
      |
      v
Infrastructure
      |
      +-- LLM
      +-- File System
      +-- Code Search
      +-- dotnet build/test
      +-- Git CLI
```

`Domain` không phụ thuộc Infrastructure. `Application` chỉ phụ thuộc abstraction. `Infrastructure` implement các abstraction.

## 2. Yêu cầu môi trường

- .NET SDK 8.x
- Git
- Node.js 20+ nếu muốn chạy Web UI hoặc build VS Code extension
- VS Code nếu muốn dùng extension
- API key của LLM nếu muốn dùng provider thật

Kiểm tra:

```bash
dotnet --version
git --version
node --version
npm --version
```

## 3. Build solution

Từ thư mục root:

```bash
dotnet restore
dotnet build
dotnet test
```

## 4. Chạy CLI offline

Mặc định provider là `Mock`.

```bash
dotnet run --project src/AICodingAgent.Cli -- \
  run \
  --workspace ./sample \
  --requirement ./requirements/example.md
```

Mock provider được dùng để kiểm tra wiring/workflow. Nó KHÔNG tự sinh code thật.

## 5. Chạy với OpenAI

Set environment variables.

### Windows PowerShell

```powershell
$env:AICODINGAGENT__PROVIDER="OpenAI"
$env:AICODINGAGENT__MODEL="gpt-5.6-luna"
$env:AICODINGAGENT__APIKEY="YOUR_API_KEY"
$env:AICODINGAGENT__BASEURL="https://api.openai.com/v1"
```

Sau đó:

```powershell
dotnet run --project src/AICodingAgent.Cli -- `
  run `
  --workspace "C:\Projects\MyProject" `
  --requirement "C:\Projects\MyProject\requirements\ABC-123.md"
```

### Linux/macOS

```bash
export AICODINGAGENT__PROVIDER=OpenAI
export AICODINGAGENT__MODEL=gpt-5.6-luna
export AICODINGAGENT__APIKEY='YOUR_API_KEY'
export AICODINGAGENT__BASEURL='https://api.openai.com/v1'

dotnet run --project src/AICodingAgent.Cli -- \
  run \
  --workspace ./sample \
  --requirement ./requirements/example.md
```

Provider code sử dụng OpenAI Responses API thông qua abstraction `ILanguageModel`. Có thể thay provider bằng implementation khác mà không đổi workflow.

## 6. Chạy API

```bash
dotnet run --project src/AICodingAgent.Api --urls http://localhost:5099
```

Health check:

```text
http://localhost:5099/health
```

## 7. Web UI

The React web client lets you enter a project workspace and requirement, review
analysis questions, answer them in multiple rounds, and run the implementation
workflow once the requirement is ready.

Start the API and web client in separate terminals:

```bash
dotnet run --project src/AICodingAgent.Api --urls http://localhost:5099
```

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:5173`. The API URL defaults to `http://localhost:5099`
and can be changed in the UI. The API allows browser requests from the local
Vite development origin (`http://localhost:5173`).

## 8. Analyze requirement bằng API

```bash
curl -X POST http://localhost:5099/api/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Customer Search",
    "source": "requirements/example.md",
    "workspacePath": "."
  }'
```

Hoặc gửi text trực tiếp:

```bash
curl -X POST http://localhost:5099/api/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Customer Search",
    "content": "Implement customer search by code and name.",
    "workspacePath": "."
  }'
```

## 9. Chạy full workflow

```bash
curl -X POST http://localhost:5099/api/run \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Customer Search",
    "source": "requirements/example.md",
    "workspacePath": ".",
    "autoCommit": false
  }'
```

`autoCommit=false` là cấu hình an toàn mặc định.

Nếu muốn Agent tạo branch và commit:

```json
{
  "autoCommit": true
}
```

Chỉ bật khi workspace Git đã được kiểm soát.

## 10. VS Code extension

Extension nằm tại:

```text
plugins/vscode/ai-coding-agent
```

Build:

```bash
cd plugins/vscode/ai-coding-agent
npm install
npm run compile
```

Sau đó mở folder extension trong VS Code và nhấn `F5`.

Trước khi dùng extension, chạy API:

```bash
dotnet run --project src/AICodingAgent.Api --urls http://localhost:5099
```

Extension gọi:

```text
POST /api/analyze
POST /api/run
```

API base URL mặc định:

```text
http://localhost:5099
```

Có thể đổi trong VS Code setting:

```text
AI Coding Agent: Api Base Url
```

## 11. Requirement URL

API/CLI có thể nhận HTTP/HTTPS URL ở `source`.

Ví dụ:

```json
{
  "source": "https://example.com/requirement.txt"
}
```

Trong production nên thêm authentication/connector riêng cho Jira, Azure DevOps, GitHub, SharePoint thay vì cho Agent truy cập URL tùy ý.

## 12. Git safety

Mặc định CLI không commit.

Chỉ dùng:

```bash
--auto-commit
```

khi muốn workflow:

```text
create feature branch
    |
modify files
    |
build
    |
test
    |
review
    |
git add -A
    |
git commit
```

Không có chức năng force push trong MVP.

## 13. Security

Không đưa API key vào source code.

Không commit:

```text
appsettings.Local.json
.env
```

Nên dùng environment variables hoặc secret manager.

MVP đã có path traversal protection ở `FileSystemWorkspace`.

LLM chỉ được yêu cầu thao tác trên file nằm trong workspace.

## 14. Giới hạn MVP

MVP này đã có workflow thực thi thật nhưng vẫn còn những phần nên làm trước khi dùng production:

- Roslyn semantic code search.
- Incremental patch thay vì trả full file.
- Persistent AgentSession database.
- SignalR event stream.
- Human approval UI.
- MCP server.
- Jira/Azure DevOps/GitHub connectors.
- Better structured outputs/schema validation.
- Sandbox cho build/test.
- Secret redaction.
- Token/context budget manager.
- Parallel tool execution.
- Multi-attempt repair loop.
- PR creation.
- Visual Studio VSIX hoàn chỉnh.
- VS Code webview/task UI.

## 15. Roadmap

### Phase 1
- Domain/Application/Infrastructure
- OpenAI provider
- Requirement analysis
- Plan
- Code generation
- Build/test/review
- Git

### Phase 2
- SQLite persistence
- SignalR
- Approval workflow
- Webview UI
- Audit log

### Phase 3
- Roslyn
- MCP
- Jira/Azure DevOps/GitHub
- PR automation

### Phase 4
- Visual Studio extension
- CI/CD agent
- Enterprise policy
- RBAC
- Organization-wide coding rules

## 16. Production recommendation

Không nên cho Agent quyền:

```text
arbitrary shell
force push
delete repository
access arbitrary filesystem
access arbitrary network
```

Nên expose các tool có schema rõ ràng:

```text
read_file
search_code
find_symbol
apply_patch
build
test
git_status
git_diff
git_create_branch
git_commit
```

và mỗi tool có policy/approval riêng.

## 17. Cấu trúc source

```text
AICodingAgent
|
+-- src
|   +-- AICodingAgent.Domain
|   +-- AICodingAgent.Application
|   +-- AICodingAgent.Infrastructure
|   +-- AICodingAgent.Api
|   +-- AICodingAgent.Cli
|
+-- tests
|   +-- AICodingAgent.Application.Tests
|
+-- plugins
|   +-- vscode
|
+-- requirements
|
+-- README.md
```

## 18. Important

Đây là một MVP source code có thể dùng làm nền tảng phát triển thật. Không nên coi `MockLanguageModel` là AI implementation; để Agent tự implement code thật cần cấu hình provider LLM thật.

OpenAI provider hiện tại nằm ở:

```text
src/AICodingAgent.Infrastructure/LLM/OpenAiLanguageModel.cs
```

và workflow chính ở:

```text
src/AICodingAgent.Application/Workflows/SoftwareImplementationWorkflow.cs
```

Nếu thay LLM provider, chỉ cần implement:

```csharp
public interface ILanguageModel
{
    Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken);
}
```

Workflow không cần thay đổi.

***************DEMO********************

Run flow: https://localhost:63478/api/run
Request:
// {
//     "title": "Customer Search",
//     "content": "Implement customer search by code and name.",
//     //"source": "C:\\Users\\PhiVo\\Downloads\\AICodingAgent\\AICodingAgent\\requirements\\example.md",
//     "workspacePath": "C:\\Users\\PhiVo\\Downloads\\AICodingAgent\\AICodingAgent\\sample",
//     "autoCommit": false
//   }
Response: if state: 3 nghĩa là WaitingForClarification theo AgentState. Workflow dừng trước khi tạo plan vì isReadyForImplementation đang là false; plan, implementation, build và các bước sau vì vậy đều null. Hiện API chưa có endpoint để gửi câu trả lời tiếp nối session, nên hãy gửi lại POST /api/run với yêu cầu đã bổ sung trong

Nếu chỉ muốn phân tích mà chưa chạy workflow, dùng POST /api/analyze với body tương tự; autoCommit không cần thiết cho endpoint đó.
