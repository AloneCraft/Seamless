solution Seamless
├── Seamless.Core (Interfaces, Common Abstractions)
├── Seamless.Analyzers (Source Generator, Code Fixers)
│
├── Seamless.Tsavorite (Storage Engine wrapper, IFunctions)
│   └── Seamless.Tsavorite.MemoryPack (Serializer integration)
│
├── Seamless.Collections (Persistent Dictionary, Set, Queue)
│
├── Seamless.Reasoning (Benchmark-driven Topology Inference)
│
├── Seamless.Database (The Core Logic, Relational mapping)
│
└── Seamless.RelationStudio (The UI / Management Studio)
最高品質のTsavorite,最高品質のMemoryPackは対等である。だから
Seamless.Tsavorite
Seamless.MemoryPack
しかし
Seamless.Collections:System.Collections.Generic,System.Collections.Concurrentはやらないほうがいいか？


最終的なプロジェクト計画をきれいにするため
solution Seamless
	project Seamless.RelationStudio Seamless.DatabaseをUIで表示する
		参照するプロジェクトSeamless.Database
		namespace Seamless.Ui
	project Seamless.Database
		参照するプロジェクトSeamless.Collections
		namespace Seamless.Database
			メインアプリケーション。もっともでかく価値のあるもの。
	project Seamless.Collections 
		参照するプロジェクトSeamless.Tsavorite
		namespace Seamless.Collections
			Seamless.Databaseで使う。他でも使えるのでプロジェクト切り出し
	project Seamless.Tsavorite
		参照するプロジェクトMemryPack,Seamless.Core
		namespace Seamless.Tsavorite
			バックエンド。ストレージエンジン。
		namespace Seamless.Tsavorite.MemoryPack
			Seamless.Tsavoriteで使うMemoryPackなのでここにおく
	project Seamless.Core 基本機能
	project Seamless.Reasoning ベンチマークしてトポロジーを推論する
		namespace Seamless.Reasoning
			Seamless.Tsavoriteで使う。他でも使えるのでプロジェクト切り出し
	project Seamless.Analizers
		namespace Seamless.Analizers
			ソースジェネレータ―
	project Seamless.Reasoning ベンチマークしてトポロジーを推論する
		namespace Seamless.Reasoning
			Seamless.Tsavoriteで使う。他でも使えるのでプロジェクト切り出し
	
