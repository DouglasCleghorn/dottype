const vscode = require('vscode');
const fs = require('node:fs');
const path = require('node:path');

exports.run = async function () {
  const root = process.env.dottype_EDITOR_TEST_DIR;
  const resultPath = process.env.dottype_EDITOR_TEST_RESULT;
  const format = process.env.dottype_EDITOR_TEST_FORMAT || 'json';
  const result = { format, beforeFirstRun: false, refresh: false, invalidInput: false, recovery: false };
  const uri = vscode.Uri.file(path.join(root, 'app.cs'));
  try {
    const document = await vscode.workspace.openTextDocument(uri);
    await vscode.window.showTextDocument(document);
    const position = document.positionAt(document.getText().indexOf('customer.);') + 'customer.'.length);
    async function waitForMember(name, seconds) {
      for (let i = 0; i < seconds; i++) {
        const completions = await vscode.commands.executeCommand('vscode.executeCompletionItemProvider', uri, position, '.');
        const labels = (completions?.items ?? []).map(item => typeof item.label === 'string' ? item.label : item.label.label);
        result.lastCompletions = labels;
        if (labels.includes(name)) return;
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      throw new Error(`Timed out waiting for completion '${name}'`);
    }
    await waitForMember('id', 90);
    result.beforeFirstRun = true;
    fs.writeFileSync(resultPath, JSON.stringify(result, null, 2));
    const exampleUri = vscode.Uri.file(path.join(root, 'sample.' + format));
    const changed = fs.readFileSync(path.join(root, 'changed.' + format));
    if (format === 'parquet') {
      await vscode.workspace.fs.writeFile(exampleUri, changed);
    } else {
      const example = await vscode.workspace.openTextDocument(exampleUri);
      const edit = new vscode.WorkspaceEdit();
      edit.replace(exampleUri, new vscode.Range(example.positionAt(0), example.positionAt(example.getText().length)), changed.toString('utf8'));
      await vscode.workspace.applyEdit(edit);
      await example.save();
    }
    await waitForMember('displayName', 45);
    result.refresh = true;
    await vscode.workspace.fs.writeFile(exampleUri, Buffer.from(format === 'csv' ? 'id\n"unterminated' : 'invalid'));
    for (let i = 0; i < 30; i++) {
      if (vscode.languages.getDiagnostics(uri).some(d => (d.code?.value ?? d.code) === 'dottype001')) {
        result.invalidInput = true;
        break;
      }
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (!result.invalidInput) throw new Error('Missing editor diagnostic for invalid example');
    await vscode.workspace.fs.writeFile(exampleUri, changed);
    await waitForMember('displayName', 30);
    result.recovery = true;
  } catch (error) {
    result.error = String(error.stack ?? error);
    result.diagnostics = vscode.languages.getDiagnostics(uri).map(d => ({ message: d.message, code: d.code }));
    throw error;
  } finally {
    fs.writeFileSync(resultPath, JSON.stringify(result, null, 2));
  }
};
