# The Island - 开发路线图

## 总览

```
Phase 1-7    Phase 8      Phase 9       Phase 10-18     Phase 19        未来
 核心基础    VFX/打赏     AI导演/投票    生存/社交/制作    视觉打磨       Phase 20+
 [x] 已完成   [x] 已完成    [x] 已完成     [x] 已完成      [x] 已完成     [ ] 待规划
```

## 已完成阶段

### Phase 1-7: 核心基础系统
- [x] Phase 1: WebSocket 实时通信后端 MVP
- [x] Phase 2: 基础 RPG 机制 (HP/战斗)
- [x] Phase 3: 转型荒岛生存模拟 + SQLite 持久化
- [x] Phase 4: LLM 集成 (AI 角色对话生成)
- [x] Phase 5: Unity 6 客户端 + 2.5D 视觉系统
- [x] Phase 6: 多 LLM 提供商支持 (LiteLLM)
- [x] Phase 7: 游戏性增强 + Twitch 直播集成 + 中文字体

### Phase 8: VFX 粒子特效 + AI 打赏反应系统
- [x] 天气粒子系统 (雨、雾、热浪)
- [x] Bits 打赏 → 金币转换 + 特效
- [x] 程序化海滩场景 (海洋、天空、地面)

### Phase 9: AI 导演系统 + 叙事投票
- [x] 四种游戏模式：simulation / narrative / voting / resolution
- [x] AI 导演自动生成剧情事件 (PlotPoint)
- [x] Twitch 观众投票 (!1 / !2) 决定剧情走向
- [x] 投票倒计时 + 实时进度广播
- [x] 剧情决议应用 (增益/减益/资源变化)

### Phase 10-18: 生存、社交、制作、记忆系统
- [x] **生存机制**: HP/能量/心情 三大属性 + 饥饿/疾病
- [x] **昼夜系统**: 黎明 → 白天 → 黄昏 → 夜晚
- [x] **天气系统**: 晴天/多云/雨天/暴风雨/炎热/雾天
- [x] **社交系统**: leader/follower/loner 角色 + 自主互动 + 利他行为
- [x] **制作系统**: 采集草药 → 制作药品 → 治疗疾病
- [x] **记忆系统**: Agent 记忆重要互动和事件
- [x] **自主行动**: 采集/休息/社交/篝火聚集
- [x] **随机事件**: 风暴破坏/发现宝藏/野兽袭击
- [x] **NavMesh 路径寻找**: Unity 端角色移动
- [x] **休闲模式**: 自动复活 + 降低难度

### Phase 19: 视觉打磨与 Cinematic 光照
- [x] 19-A: 程序化角色精灵生成 + Billboard UI
- [x] 19-B: 渐变天空盒 + 动态光照
- [x] 19-C: 精灵资源加载
- [x] 19-D: 透明度 + 动画系统 + 速度感知动画
- [x] 19-E: 社交行为可视化 + 脚印粒子

---

## 待规划阶段

### Phase 20: 测试与质量保障
- [ ] 后端 pytest 单元测试 (目标 80% 覆盖)
- [ ] WebSocket 集成测试
- [ ] Unity Play Mode 测试
- [ ] E2E 测试 (完整游戏循环)
- [ ] CI/CD pipeline (GitHub Actions)

### Phase 21: 架构优化
- [ ] engine.py 拆分 (当前 2184 行，超过 800 行限制)
  - `engine.py` → `game_loop.py` + `command_handler.py` + `social_system.py` + `survival_system.py`
- [ ] `director_service.py` 拆分 (562 行)
- [ ] 配置外部化 (魔法数字 → YAML/TOML 配置文件)
- [ ] 结构化日志 (替换 print / 统一 log level)

### Phase 22: 持久化升级
- [ ] SQLite → PostgreSQL 迁移 (支持并发写入)
- [ ] 数据库迁移工具 (Alembic)
- [ ] Redis 缓存层 (高频状态读取)

### Phase 23: 新玩法系统
- [ ] 建筑系统 (建造庇护所/工具)
- [ ] 贸易系统 (角色间物品交换)
- [ ] 技能树 (采集/制作/社交专精)
- [ ] 季节系统 (春/夏/秋/冬，影响资源产出)

### Phase 24: 多人增强
- [ ] 多玩家独立金币账户
- [ ] 玩家排行榜
- [ ] 团队协作任务
- [ ] PvP 干扰机制

### Phase 25: 运营工具
- [ ] 管理后台 (Web Dashboard)
- [ ] 游戏数据分析面板
- [ ] A/B 测试框架 (不同 LLM prompt 效果对比)
- [ ] 自动化部署 (Docker + docker-compose)

---

## 技术债务

| 优先级 | 项目 | 说明 |
|--------|------|------|
| P0 | 无测试覆盖 | 0% 测试覆盖率，任何修改都有回归风险 |
| P0 | engine.py 2184 行 | 单文件超过限制 2.7 倍，需拆分 |
| P1 | 无类型检查 | 缺少 mypy/pyright 配置 |
| P1 | 无 CI/CD | 无自动化构建/测试/部署 |
| P2 | 魔法数字 | 大量硬编码值分散在 engine.py 中 |
| P2 | 无日志轮转 | 日志文件无限增长 |
| P3 | C# 脚本组织 | Unity Scripts 目录扁平，建议按功能分文件夹 |

---

## 里程碑时间线

```
2025-12-30  MVP 原型
2026-01-01  核心系统完成 (Phase 1-8)
2026-01-02  高级系统完成 (Phase 9-19)
  ↓
  现在      Phase 20+ 待启动
```
