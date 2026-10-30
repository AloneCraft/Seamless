import unittest
from audit import inspect_text,inspect_project,inspect_assets,run
import tempfile,pathlib
class Audit(unittest.TestCase):
    def test_source_and_namespace_clone_are_candidates(self):
        self.assertEqual([1],inspect_text("using Microsoft.SqlServer.TransactSql.ScriptDom;"))
        self.assertEqual([1],inspect_text("namespace Microsoft.SqlServer.TransactSql.ScriptDom;"))
        self.assertEqual([1],inspect_text("var parser=new TSql170Parser(true);"))
        self.assertEqual([],inspect_text("namespace Seamless.Sql;"))
    def test_exact_case_insensitive_package_and_xml_namespace(self):
        self.assertEqual(1,len(inspect_project('<Project xmlns="urn:msbuild"><ItemGroup><PackageReference Include="Microsoft.SqlServer.TransactSql.ScriptDom" /></ItemGroup></Project>')))
        self.assertEqual([],inspect_project('<Project><PackageReference Include="Seamless.Sql" /></Project>'))
    def test_transitive_package_in_restored_assets(self):
        self.assertEqual(["Microsoft.SqlServer.TransactSql.ScriptDom/180.102.0"],inspect_assets('{"libraries":{"Microsoft.SqlServer.TransactSql.ScriptDom/180.102.0":{},"Seamless.Sql/1.0":{}}}'))
        self.assertEqual([],inspect_assets('{"libraries":{}}'))
    def test_generated_artifacts_are_not_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root=pathlib.Path(directory)
            (root/"Artifacts").mkdir()
            (root/"Artifacts"/"broken.csproj").write_text("not XML",encoding="utf-8")
            (root/".claude").mkdir()
            (root/".claude"/"old.csproj").write_text('<Project><PackageReference Include="Microsoft.SqlServer.TransactSql.ScriptDom" /></Project>',encoding="utf-8")
            (root/"Example.cs").write_text("using Microsoft.SqlServer.TransactSql.ScriptDom;",encoding="utf-8")
            result=run(root,[])
            self.assertEqual([],result["errors"])
            self.assertEqual(1,result["candidateFiles"])
if __name__=="__main__":unittest.main()
