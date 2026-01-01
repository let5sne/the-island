# The Island - 荒岛生存模拟游戏

一个实时多人互动的荒岛生存模拟游戏，玩家可以通过命令与 AI 角色互动，帮助他们在荒岛上生存。

## 项目架构

```
the-island/
├── backend/           # Python FastAPI 后端服务
│   └── app/
│       ├── main.py       # 应用入口
│       ├── server.py     # WebSocket 服务器
│       ├── engine.py     # 游戏引擎核心逻辑
│       ├── models.py     # SQLAlchemy 数据模型
│       ├── schemas.py    # Pydantic 消息模式
│       ├── llm.py        # LLM 集成 (对话生成)
│       └── database.py   # 数据库配置
├── frontend/          # Web 调试客户端
│   ├── app.js           # JavaScript 客户端
│   └── debug_client.html # 调试页面
├── unity-client/      # Unity 6 游戏客户端
│   └── Assets/
│       ├── Scripts/     # C# 游戏脚本
│       ├── Fonts/       # 字体资源 (含中文支持)
│       └── Editor/      # 编辑器工具
└── island.db          # SQLite 数据库
```

## 功能特性

### 游戏系统
- **生存机制**: 角色有 HP、能量、心情三大属性
- **昼夜循环**: 黎明 → 白天 → 黄昏 → 夜晚
- **天气系统**: 晴天、多云、雨天、暴风雨、炎热、雾天
- **社交系统**: 角色间自主社交互动
- **休闲模式**: 自动复活、降低难度

### 玩家命令
| 命令 | 格式 | 金币消耗 | 效果 |
|------|------|----------|------|
| feed | `feed <角色名>` | 10g | +20 能量, +5 HP |
| heal | `heal <角色名>` | 15g | +30 HP |
| talk | `talk <角色名> [话题]` | 0g | 与角色对话 |
| encourage | `encourage <角色名>` | 5g | +15 心情 |
| revive | `revive <角色名>` | 10g | 复活死亡角色 |
| check | `check` | 0g | 查看所有状态 |
| reset | `reset` | 0g | 重置游戏 |

### AI 角色
- **Jack** (勇敢) - 蓝色
- **Luna** (狡猾) - 粉色
- **Bob** (诚实) - 绿色

每个角色有独特性格，会根据性格做出不同反应和社交行为。

## 技术栈

### 后端
- **Python 3.11+**
- **FastAPI** - 异步 Web 框架
- **WebSocket** - 实时双向通信
- **SQLAlchemy** - ORM 数据持久化
- **SQLite** - 轻量级数据库
- **Anthropic Claude** - LLM 对话生成

### Unity 客户端
- **Unity 6 LTS** (6000.3.2f1)
- **TextMeshPro** - 高质量文本渲染
- **NativeWebSocket** - WebSocket 通信
- **2.5D 风格** - 精灵 + Billboard UI

## 快速开始

### 1. 启动后端服务

```bash
cd backend
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

### 2. 启动 Unity 客户端

1. 使用 Unity 6 打开 `unity-client` 文件夹
2. 打开 `Assets/Scenes/main.unity`
3. 点击 Play 运行游戏

### 3. Web 调试客户端 (可选)

在浏览器打开 `frontend/debug_client.html`

## Unity 客户端结构

### 核心脚本
| 脚本 | 功能 |
|------|------|
| `NetworkManager.cs` | WebSocket 连接管理、消息收发 |
| `GameManager.cs` | 游戏状态管理、角色生成 |
| `UIManager.cs` | 主 UI 界面 (顶部状态栏、底部命令输入) |
| `EventLog.cs` | 事件日志面板 (显示游戏事件) |
| `AgentVisual.cs` | 角色视觉组件 (精灵、血条、对话框) |
| `EnvironmentManager.cs` | 环境场景 (沙滩、海洋、天空) |
| `WeatherEffects.cs` | 天气粒子效果 (雨、雾、热浪) |

### 视觉特性
- 程序化生成的 2.5D 角色精灵
- Billboard UI (始终面向摄像机)
- 动态天气粒子系统
- 渐变天空盒 (随时间变化)
- 海浪动画效果

## 中文字体支持

项目使用 **思源黑体 (Source Han Sans SC)** 支持中文显示。

### 手动配置步骤
1. 选择 `Assets/Fonts/SourceHanSansSC-Regular.otf`
2. 右键 → Create → TextMeshPro → Font Asset → SDF
3. 打开 Edit → Project Settings → TextMesh Pro
4. 在 Fallback Font Assets 中添加生成的字体资产

## 通信协议

### WebSocket 端点
```
ws://localhost:8000/ws/{username}
```

### 事件类型
```python
# 核心事件
TICK            # 游戏心跳
AGENTS_UPDATE   # 角色状态更新
AGENT_SPEAK     # 角色发言
AGENT_DIED      # 角色死亡

# 时间系统
PHASE_CHANGE    # 时段变化 (黎明/白天/黄昏/夜晚)
DAY_CHANGE      # 新的一天
WEATHER_CHANGE  # 天气变化

# 玩家互动
FEED            # 喂食反馈
HEAL            # 治疗反馈
TALK            # 对话反馈
ENCOURAGE       # 鼓励反馈
REVIVE          # 复活反馈

# 社交系统
SOCIAL_INTERACTION  # 角色间社交
AUTO_REVIVE         # 自动复活 (休闲模式)
```

## 环境变量

创建 `.env` 文件:
```env
ANTHROPIC_API_KEY=your_api_key_here
```

## 开发说明

### 添加新命令
1. 在 `backend/app/schemas.py` 添加事件类型
2. 在 `backend/app/engine.py` 添加命令处理逻辑
3. 在 `unity-client/Assets/Scripts/Models.cs` 添加数据模型
4. 在 `unity-client/Assets/Scripts/NetworkManager.cs` 添加事件处理

### 调试
- Unity 控制台查看日志
- Web 调试客户端查看原始消息
- 后端日志查看服务器状态

## 许可证

MIT License
