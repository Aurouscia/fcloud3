# 目标格式
*文中提到的标识符均来自/lib/common/marks*
## 基本要求 
- 正确目标的每一行应该都有内容（空格不算）
## 首行
- 首行必须有关键字:AuRtRd，识别到之后将其删掉，不影响后续解析
## 每行内
- 目标的每一行由seperator分为几个部分
    - 第一部分为主要描述，描述这块应该绘制什么东西
    - 第二部分及以后是一个或多个标注(annotation)
## 配置
- AuRtRd后若有(xxx)必须是半角括号，则里面的部分会被识别为配置
    - 配置必须以configSeperator开头和结尾，中间为数个由configSeperator隔开的键值对组成
    - 键值对由configKvSeperator分开