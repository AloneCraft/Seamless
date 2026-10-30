# Staged SQL branch integration (2026-10-30)

Merge source: 14fca7479293d520f64a847ae11029d8449e6e91.
Target before merge: 883e2f277fcb59426797fdef1fe92d087b344da8.

The source is a late commit of an earlier index snapshot. Its parent 481fe149 is represented by 93d1ff58 in the existing rewrite map; that mapped commit is reachable from the target.

All 238 changed paths were compared with the approved working-tree snapshot 9bb95f46 and the target. 147 are identical. 76 staged versions were subsequently updated in the approved snapshot and SQL/SSMS followup. Ten solution/project-list files retain the regenerated integrated registrations. Two obsolete generated SSMS view scripts remain deleted. One catalog test retains its later heap-index correction. Two protocol test files retain the accepted subset; the four unfinished cases remain preserved in ../branch-integration-20261030/held-protocol-tests.patch and described in held-protocol-tests.md.

The resulting product tree equals the target tree: no additional product change was justified by this older staged snapshot. This is a reviewed resolution, not an assertion that the four pending acceptance cases pass. Per-path blob IDs and decisions are in review.json.

A normal two-parent merge was started and its conflicts resolved to these reviewed versions. The original source commit and all its ancestors become reachable through the second parent. Existing rewritten merges remain reachable through the first parent. No release tag or remote branch is moved by this operation.
