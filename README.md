# The Island - 荒岛生存模拟游戏

一个实时多人互动的荒岛生存模拟游戏，玩家可以通过命令与 AI 角色互动，帮助他们在荒岛上生存。

## 项目架构

```
the-island/
├── backend/           # Python FastAPI 后端服务
│   └── app/
│       ├── main.py          # 应用入口
│       ├── server.py        # WebSocket 服务器
│       ├── engine.py        # 游戏引擎核心逻辑
│       ├── models.py        # SQLAlchemy 数据模型
│       ├── schemas.py       # Pydantic 消息模式
│       ├── llm.py           # LLM 集成 (对话生成)
│       ├── memory_service.py # Agent 记忆管理服务
│       ├── twitch_service.py # Twitch 聊天机器人
│       ├── director_service.py # AI 导演服务 (叙事生成)
│       ├── vote_manager.py  # 投票管理器
│       └── database.py      # 数据库配置
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
- **自主行动**: 角色会自动进行采集、休息、社交等行为
- **疾病机制**: 恶劣天气和低免疫力可能导致生病
- **制作系统**: 使用草药制作药品治愈疾病
- **资源稀缺**: 树木果实有限，每日再生
- **社交角色**: 领导者、追随者、独行者动态关系
- **记忆系统**: Agent 会记住重要的互动和事件
- **随机事件**: 风暴破坏、发现宝藏、野兽袭击等
- **AI 导演系统**: 自动生成剧情事件，观众投票决定剧情走向
- **叙事投票**: Twitch 观众通过 `!1` `!2` 命令参与剧情决策

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
| !1 / !A | `!1` 或 `!A` | 0g | 投票选择第一选项 |
| !2 / !B | `!2` 或 `!B` | 0g | 投票选择第二选项 |

### AI 角色
- **Jack** (勇敢) - 蓝色
- **Luna** (狡猾) - 粉色
- **Bob** (诚实) - 绿色

每个角色有独特性格，会根据性格做出不同反应和社交行为。

#### 角色属性
| 属性 | 说明 |
|------|------|
| HP | 生命值，归零则死亡 |
| 能量 | 行动力，过低会影响行动 |
| 心情 | 情绪状态，影响社交和决策 |
| 免疫力 | 抵抗疾病的能力 (0-100) |
| 社交角色 | leader/follower/loner/neutral |
| 当前行动 | Idle/Gather/Sleep/Socialize 等 |
| 位置 | tree_left/tree_right/campfire/herb_patch 等 |

## 技术栈

### 后端
- **Python 3.11+**
- **FastAPI** - 异步 Web 框架
- **WebSocket** - 实时双向通信
- **SQLAlchemy** - ORM 数据持久化
- **SQLite** - 轻量级数据库
- **LiteLLM** - 多 LLM 提供商支持
- **TwitchIO** - Twitch 聊天集成

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
| `GameManager.cs` | 游戏状态管理、角色生成、行动系统 |
| `UIManager.cs` | 主 UI 界面 (顶部状态栏、底部命令输入) |
| `EventLog.cs` | 事件日志面板 (显示游戏事件) |
| `AgentVisual.cs` | 角色视觉组件 (精灵、血条、对话框、状态图标) |
| `EnvironmentManager.cs` | 环境场景 (沙滩、海洋、天空) |
| `WeatherEffects.cs` | 天气粒子效果 (雨、雾、热浪) |
| `Models.cs` | 数据模型 (Agent、WorldState、事件数据) |
| `NarrativeUI.cs` | AI 导演叙事界面 (剧情卡片、投票进度条、倒计时) |

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
GIFT_EFFECT     # Bits 打赏特效

# 社交系统
SOCIAL_INTERACTION  # 角色间社交
AUTO_REVIVE         # 自动复活 (休闲模式)

# 自主行动系统 (Phase 13+)
AGENT_ACTION    # 角色执行行动 (采集/休息/社交等)
CRAFT           # 制作物品 (药品等)
USE_ITEM        # 使用物品
RANDOM_EVENT    # 随机事件 (风暴/宝藏/野兽等)

# AI 导演与叙事投票 (Phase 9)
MODE_CHANGE         # 游戏模式切换 (simulation/narrative/voting/resolution)
NARRATIVE_PLOT      # 导演生成的剧情事件
VOTE_STARTED        # 投票开始
VOTE_UPDATE         # 实时投票进度更新
VOTE_ENDED          # 投票结束
VOTE_RESULT         # 投票结果
RESOLUTION_APPLIED  # 剧情决议执行
```

## Twitch 直播集成

游戏支持连接到 Twitch 直播聊天室，观众可以通过发送弹幕来控制游戏。

### 获取 Twitch Token

**方法一：Twitch Token Generator (推荐用于测试)**
1. 访问 https://twitchtokengenerator.com/
2. 选择 "Bot Chat Token"
3. 使用你的 Twitch 账号授权
4. 复制 "Access Token" (以 `oauth:` 开头)

**方法二：Twitch Developer Console (生产环境)**
1. 访问 https://dev.twitch.tv/console/apps
2. 创建新应用，类型选择 "Chat Bot"
3. 设置 OAuth 重定向 URL: `http://localhost:3000`
4. 使用 OAuth 授权码流程获取 Token
5. 需要的权限范围: `chat:read`, `chat:edit`, `bits:read`

### Bits 打赏转换

观众在直播间使用 Bits 打赏时，系统会自动将 Bits 转换为游戏内金币：
- **转换比率**: 1 Bit = 1 Gold
- Unity 客户端会收到 `gift_effect` 事件用于显示特效

## 环境变量

在 `backend/.env` 文件中配置：

```env
# LLM 配置 (选择一种)
ANTHROPIC_API_KEY=your_api_key_here
# 或
OPENAI_API_KEY=your_api_key_here
LLM_MODEL=gpt-3.5-turbo

# Twitch 配置 (可选)
TWITCH_TOKEN=oauth:your_access_token_here
TWITCH_CHANNEL_NAME=your_channel_name
TWITCH_COMMAND_PREFIX=!
```

### 必需变量
| 变量 | 说明 |
|------|------|
| `LLM_MODEL` | LLM 模型名称 |
| `ANTHROPIC_API_KEY` 或 `OPENAI_API_KEY` | LLM API 密钥 |

### Twitch 变量 (可选)
| 变量 | 说明 |
|------|------|
| `TWITCH_TOKEN` | OAuth Token (必须以 `oauth:` 开头) |
| `TWITCH_CHANNEL_NAME` | 要加入的频道名称 |
| `TWITCH_COMMAND_PREFIX` | 命令前缀 (默认 `!`) |

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
