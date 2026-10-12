import * as vscode from "vscode";

async function post(
    path: string,
    body: unknown
): Promise<unknown> {
    const baseUrl = vscode.workspace
        .getConfiguration("aicodingagent")
        .get<string>("apiBaseUrl", "http://localhost:5099");

    const response = await fetch(`${baseUrl}${path}`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
    });

    if (!response.ok) {
        throw new Error(
            `AI Coding Agent API failed: ${response.status} ${await response.text()}`
        );
    }

    return response.json();
}

export function activate(context: vscode.ExtensionContext) {
    const analyze = vscode.commands.registerCommand(
        "aicodingagent.analyzeRequirement",
        async () => {
            const uri = await vscode.window.showOpenDialog({
                canSelectMany: false,
                filters: {
                    "Requirement": ["md", "txt", "json"]
                }
            });

            if (!uri || uri.length === 0) {
                return;
            }

            const workspaceFolder =
                vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

            if (!workspaceFolder) {
                vscode.window.showErrorMessage(
                    "Open a workspace before running the AI Coding Agent."
                );
                return;
            }

            try {
                const result = await post("/api/analyze", {
                    source: uri[0].fsPath,
                    workspacePath: workspaceFolder
                });

                const document = await vscode.workspace.openTextDocument({
                    language: "json",
                    content: JSON.stringify(result, null, 2)
                });

                await vscode.window.showTextDocument(document);
            } catch (error) {
                vscode.window.showErrorMessage(String(error));
            }
        }
    );

    const run = vscode.commands.registerCommand(
        "aicodingagent.run",
        async () => {
            const uri = await vscode.window.showOpenDialog({
                canSelectMany: false,
                filters: {
                    "Requirement": ["md", "txt", "json"]
                }
            });

            if (!uri || uri.length === 0) {
                return;
            }

            const workspaceFolder =
                vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

            if (!workspaceFolder) {
                vscode.window.showErrorMessage(
                    "Open a workspace before running the AI Coding Agent."
                );
                return;
            }

            try {
                const result = await post("/api/run", {
                    source: uri[0].fsPath,
                    workspacePath: workspaceFolder,
                    autoCommit: false
                });

                const document = await vscode.workspace.openTextDocument({
                    language: "json",
                    content: JSON.stringify(result, null, 2)
                });

                await vscode.window.showTextDocument(document);
            } catch (error) {
                vscode.window.showErrorMessage(String(error));
            }
        }
    );

    context.subscriptions.push(analyze, run);
}

export function deactivate() {}
