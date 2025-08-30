import * as vscode from 'vscode';
import { DebugOpsMcpClient } from './debugOpsMcpClient';

let mcpClient: DebugOpsMcpClient | undefined;

export function activate(context: vscode.ExtensionContext) {
    console.log('DebugOpsMCP extension is activating...');

    // Initialize MCP client
    mcpClient = new DebugOpsMcpClient(context);

    // Register commands
    const startCommand = vscode.commands.registerCommand('debugops-mcp.start', async () => {
        try {
            await mcpClient?.start();
            vscode.window.showInformationMessage('DebugOpsMCP server started');
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to start DebugOpsMCP server: ${error}`);
        }
    });

    const stopCommand = vscode.commands.registerCommand('debugops-mcp.stop', async () => {
        try {
            await mcpClient?.stop();
            vscode.window.showInformationMessage('DebugOpsMCP server stopped');
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to stop DebugOpsMCP server: ${error}`);
        }
    });

    const statusCommand = vscode.commands.registerCommand('debugops-mcp.status', async () => {
        const status = mcpClient?.getStatus();
        const message = `DebugOpsMCP Server Status: ${status?.isRunning ? 'Running' : 'Stopped'}`;
        vscode.window.showInformationMessage(message);
    });

    // Register disposables
    context.subscriptions.push(startCommand, stopCommand, statusCommand);

    // Auto-start if configured
    const config = vscode.workspace.getConfiguration('debugops-mcp');
    if (config.get('autoStart', true)) {
        mcpClient.start().then(() => {
            console.log('DebugOpsMCP server auto-started');
        }).catch(error => {
            console.error('Failed to auto-start DebugOpsMCP server:', error);
        });
    }

    console.log('DebugOpsMCP extension activated');
}

export function deactivate(): Thenable<void> | undefined {
    console.log('DebugOpsMCP extension is deactivating...');
    
    if (mcpClient) {
        return mcpClient.stop();
    }
    
    return undefined;
}