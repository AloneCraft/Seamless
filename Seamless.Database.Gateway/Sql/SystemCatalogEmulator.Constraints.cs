using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Sql;

public static partial class SystemCatalogEmulator{
    internal static int ConstraintObjectId(string schema,string name){
        var hash=SHA256.HashData(Encoding.UTF8.GetBytes(schema.ToUpperInvariant()+"\0"+name.ToUpperInvariant()));return 0x20000000|(BinaryPrimitives.ReadInt32LittleEndian(hash)&0x1FFFFFFF);
    }
    private sealed record ConstraintObject(string Schema,TableCatalog Table,TableMetadata Metadata,string Name,string Type,int IndexId,object Definition,int ColumnId=0){
        internal int Id=>ConstraintObjectId(this.Schema,this.Name);
        internal int ParentId=>this.Metadata.StableObjectId??throw new InvalidOperationException("The constraint parent has no object id.");
    }
    private static IEnumerable<ConstraintObject> ConstraintObjects(IDatabaseAdapter adapter,IReadOnlyList<TableMetadata> tables){
        if(adapter is not InProcessDatabaseAdapter{SourceCatalog:{} catalog}){yield break;}
        foreach(var owner in catalog.Schemas){foreach(var table in owner.Tables){
            var metadata=tables.SingleOrDefault(item=>item.SchemaName.Equals(owner.Name,StringComparison.OrdinalIgnoreCase)&&item.TableName.Equals(table.Name,StringComparison.OrdinalIgnoreCase));if(metadata?.StableObjectId is null){continue;}
            if(table.PrimaryKey is{} primary){yield return new(owner.Name,table,metadata,primary.Name,"PK",1,primary);}
            for(var index=0;index<table.UniqueKeys.Count;index++){var key=table.UniqueKeys[index];var id=index+1;if(table.PrimaryKey is not null){id++;}yield return new(owner.Name,table,metadata,key.Name,"UQ",id,key);}
            foreach(var key in table.ForeignKeys){yield return new(owner.Name,table,metadata,key.Name,"F",0,key);}
            foreach(var check in table.CheckConstraints){yield return new(owner.Name,table,metadata,check.Name,"C",0,check);}
            foreach(var column in table.Columns.Where(static column=>column.DefaultDefinition is not null&&column.DefaultConstraintName is not null)){var field=metadata.Columns.Single(field=>field.Name==column.Name);yield return new(owner.Name,table,metadata,column.DefaultConstraintName!,"D",0,column,field.StableColumnId??field.Ordinal+1);}
        }}
    }
    private static string ConstraintDescription(string type)=>type switch{"PK"=>"PRIMARY_KEY_CONSTRAINT","UQ"=>"UNIQUE_CONSTRAINT","F"=>"FOREIGN_KEY_CONSTRAINT","C"=>"CHECK_CONSTRAINT","D"=>"DEFAULT_CONSTRAINT",_=>throw new InvalidDataException("Unknown constraint type.")};
    private static bool TryConstraintCatalog(string name,IDatabaseAdapter adapter,IReadOnlyList<TableMetadata> tables,IReadOnlyDictionary<string,int> schemas,out QueryResult result){
        if(name is not("check_constraints" or "default_constraints" or "key_constraints" or "foreign_keys" or "foreign_key_columns")){result=QueryResult.Empty;return false;}
        var objects=ConstraintObjects(adapter,tables).ToArray();if(objects.Select(static item=>item.Id).Distinct().Count()!=objects.Length){throw new InvalidDataException("The constraint catalog object identifiers collide.");}
        var rows=new List<object?[]>();ColumnMetadata[] columns;
        if(name=="foreign_key_columns"){
            columns=[Col("constraint_object_id",typeof(int),"int",0),Col("constraint_column_id",typeof(int),"int",1),Col("parent_object_id",typeof(int),"int",2),Col("parent_column_id",typeof(int),"int",3),Col("referenced_object_id",typeof(int),"int",4),Col("referenced_column_id",typeof(int),"int",5)];
            foreach(var item in objects.Where(static item=>item.Type=="F")){var key=(ForeignKeyCatalog)item.Definition;var parent=tables.Single(table=>table.SchemaName.Equals(key.ReferencedSchema,StringComparison.OrdinalIgnoreCase)&&table.TableName.Equals(key.ReferencedTable,StringComparison.OrdinalIgnoreCase));for(var index=0;index<key.Columns.Count;index++){var mapping=key.Columns[index];var source=item.Metadata.Columns.Single(column=>column.Name.Equals(mapping.Column,StringComparison.OrdinalIgnoreCase));var target=parent.Columns.Single(column=>column.Name.Equals(mapping.ReferencedColumn,StringComparison.OrdinalIgnoreCase));rows.Add([item.Id,index+1,item.ParentId,source.StableColumnId??source.Ordinal+1,parent.StableObjectId,target.StableColumnId??target.Ordinal+1]);}}
        }else{
            var common=new ColumnMetadata[]{Col("object_id",typeof(int),"int",0),new("name",typeof(string),"nvarchar",false,false,1,128),Col("schema_id",typeof(int),"int",2),Col("parent_object_id",typeof(int),"int",3),new("type",typeof(string),"char",false,false,4,2),new("type_desc",typeof(string),"nvarchar",false,false,5,60),Col("is_system_named",typeof(bool),"bit",6)};
            if(name=="check_constraints"){columns=[..common,Col("parent_column_id",typeof(int),"int",7),new("definition",typeof(string),"nvarchar",false,false,8,-1),Col("is_disabled",typeof(bool),"bit",9),Col("is_not_trusted",typeof(bool),"bit",10),Col("is_not_for_replication",typeof(bool),"bit",11)];}
            else if(name=="default_constraints"){columns=[..common,Col("parent_column_id",typeof(int),"int",7),new("definition",typeof(string),"nvarchar",false,false,8,-1)];}
            else if(name=="key_constraints"){columns=[..common,Col("unique_index_id",typeof(int),"int",7)];}
            else{columns=[..common,Col("referenced_object_id",typeof(int),"int",7),Col("key_index_id",typeof(int),"int",8),Col("delete_referential_action",typeof(byte),"tinyint",9),new("delete_referential_action_desc",typeof(string),"nvarchar",false,false,10,60),Col("update_referential_action",typeof(byte),"tinyint",11),new("update_referential_action_desc",typeof(string),"nvarchar",false,false,12,60),Col("is_disabled",typeof(bool),"bit",13),Col("is_not_trusted",typeof(bool),"bit",14),Col("is_not_for_replication",typeof(bool),"bit",15)];}
            foreach(var item in objects){
                var systemNamed=item.Definition switch{PrimaryKeyCatalog primaryDefinition=>primaryDefinition.IsSystemNamed,UniqueKeyCatalog uniqueDefinition=>uniqueDefinition.IsSystemNamed,ForeignKeyCatalog foreignDefinition=>foreignDefinition.IsSystemNamed,CheckConstraintCatalog checkDefinition=>checkDefinition.IsSystemNamed,ColumnCatalog defaultDefinition=>defaultDefinition.IsDefaultSystemNamed,_=>false};
                object?[] commonRow=[item.Id,item.Name,schemas[item.Schema],item.ParentId,item.Type,ConstraintDescription(item.Type),systemNamed];
                if(name=="check_constraints"&&item.Definition is CheckConstraintCatalog check){var parentColumnId=0;if(check.ParentColumn is{} columnName){var owner=item.Metadata.Columns.Single(column=>column.Name.Equals(columnName,StringComparison.OrdinalIgnoreCase));parentColumnId=owner.StableColumnId??owner.Ordinal+1;}rows.Add([..commonRow,parentColumnId,check.Definition,check.IsDisabled,false,false]);}
                else if(name=="default_constraints"&&item.Definition is ColumnCatalog column){rows.Add([..commonRow,item.ColumnId,column.DefaultDefinition]);}
                else if(name=="key_constraints"&&item.Type is "PK" or "UQ"){rows.Add([..commonRow,item.IndexId]);}
                else if(name=="foreign_keys"&&item.Definition is ForeignKeyCatalog key){var parent=tables.Single(table=>table.SchemaName.Equals(key.ReferencedSchema,StringComparison.OrdinalIgnoreCase)&&table.TableName.Equals(key.ReferencedTable,StringComparison.OrdinalIgnoreCase));var referenced=objects.Single(candidate=>candidate.ParentId==parent.StableObjectId&&candidate.Type is "PK" or "UQ"&&KeyColumns(candidate).Select(static column=>column.Name).SequenceEqual(key.Columns.Select(static pair=>pair.ReferencedColumn),StringComparer.OrdinalIgnoreCase));rows.Add([..commonRow,parent.StableObjectId,referenced.IndexId,ReferentialAction(key.DeleteAction),key.DeleteAction,ReferentialAction(key.UpdateAction),key.UpdateAction,false,false,false]);}
            }
        }
        result=new(columns,rows,rows.Count);return true;
    }
    private static IReadOnlyList<KeyColumnCatalog> KeyColumns(ConstraintObject item)=>item.Definition switch{PrimaryKeyCatalog primary=>primary.Columns,UniqueKeyCatalog unique=>unique.Columns,_=>throw new InvalidDataException("The object has no unique index.")};
    private static byte ReferentialAction(string action)=>action switch{"NO_ACTION"=>0,"CASCADE"=>1,"SET_NULL"=>2,"SET_DEFAULT"=>3,_=>throw new InvalidDataException("Unknown referential action.")};
}
