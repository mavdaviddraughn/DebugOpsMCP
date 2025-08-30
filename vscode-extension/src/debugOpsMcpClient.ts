import { ChildProcess, spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

export interface McpServerStatus {
    isRunning: boolean;
    processId?: number;
    startTime?: Date;
    lastError?: string;
}

export class DebugOpsMcpClient {
    private serverProcess?: ChildProcess;
    private context: vscode.ExtensionContext;
    private outputChannel: vscode.OutputChannel;
    private status: McpServerStatus = { isRunning: false };

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.outputChannel = vscode.window.createOutputChannel('DebugOpsMCP');
        context.subscriptions.push(this.outputChannel);
    }

    async start(): Promise<void> {
        if (this.status.isRunning) {
            throw new Error('DebugOpsMCP server is already running');
        }

        const serverPath = await this.getServerPath();
        if (!serverPath) {
            throw new Error('DebugOpsMCP server path not configured or found');
        }

        this.outputChannel.appendLine(`Starting DebugOpsMCP server: ${serverPath}`);

        try {
            // Spawn the .NET server process
            this.serverProcess = spawn('dotnet', [serverPath], {
                stdio: ['pipe', 'pipe', 'pipe'],
                cwd: path.dirname(serverPath)
            });

            if (!this.serverProcess) {
                throw new Error('Failed to spawn server process');
            }

            // Set up process event handlers
            this.setupProcessHandlers();

            // Update status
            this.status = {
                isRunning: true,
                processId: this.serverProcess.pid,
                startTime: new Date()
            };

            this.outputChannel.appendLine(`DebugOpsMCP server started with PID: ${this.serverProcess.pid}`);

            // Test the connection with a health check
            await this.sendHealthCheck();

        } catch (error) {
            this.status.lastError = error instanceof Error ? error.message : String(error);
            this.status.isRunning = false;
            throw error;
        }
    }

    async stop(): Promise<void> {
        if (!this.status.isRunning || !this.serverProcess) {
            return;
        }

        this.outputChannel.appendLine('Stopping DebugOpsMCP server...');

        // Gracefully terminate the server process
        this.serverProcess.kill('SIGTERM');

        // Wait a bit for graceful shutdown
        await new Promise(resolve => setTimeout(resolve, 2000));

        // Force kill if still running
        if (this.serverProcess && !this.serverProcess.killed) {
            this.serverProcess.kill('SIGKILL');
        }

        this.serverProcess = undefined;
        this.status = { isRunning: false };

        this.outputChannel.appendLine('DebugOpsMCP server stopped');
    }

    getStatus(): McpServerStatus {
        return { ...this.status };
    }

    async sendMcpRequest(request: any): Promise<any> {
        if (!this.status.isRunning || !this.serverProcess) {
            throw new Error('DebugOpsMCP server is not running');
        }

        return new Promise((resolve, reject) => {
            const requestJson = JSON.stringify(request);
            let responseData = '';

            // Set up one-time listeners for this request
            const onData = (data: Buffer) => {
                responseData += data.toString();
                const lines = responseData.split('\n');

                // Check if we have a complete response (ends with newline)
                if (lines.length > 1) {
                    this.serverProcess!.stdout!.off('data', onData);
                    this.serverProcess!.stderr!.off('data', onError);

                    try {
                        const response = JSON.parse(lines[0]);
                        resolve(response);
                    } catch (error) {
                        reject(new Error(`Failed to parse MCP response: ${error}`));
                    }
                }
            };

            const onError = (data: Buffer) => {
                this.serverProcess!.stdout!.off('data', onData);
                this.serverProcess!.stderr!.off('data', onError);
                reject(new Error(`Server error: ${data.toString()}`));
            };

            // Set up listeners
            this.serverProcess.stdout!.on('data', onData);
            this.serverProcess.stderr!.on('data', onError);

            // Send the request
            this.serverProcess.stdin!.write(requestJson + '\n');

            // Set timeout
            setTimeout(() => {
                this.serverProcess!.stdout!.off('data', onData);
                this.serverProcess!.stderr!.off('data', onError);
                reject(new Error('MCP request timeout'));
            }, 10000);
        });
    }

    private async getServerPath(): Promise<string | null> {
        // First, check configuration
        const config = vscode.workspace.getConfiguration('debugops-mcp');
        const configuredPath = config.get<string>('serverPath');

        if (configuredPath && fs.existsSync(configuredPath)) {
            return configuredPath;
        }

        // Try to find the server in the extension directory
        const extensionPath = this.context.extensionPath;
        const relativePath = path.join('..', 'core', 'src', 'DebugOpsMCP.Host', 'bin', 'Debug', 'net8.0', 'DebugOpsMCP.Host.dll');
        const serverPath = path.resolve(extensionPath, relativePath);

        if (fs.existsSync(serverPath)) {
            return serverPath;
        }

        // Try alternative path for published builds
        const publishedPath = path.join(extensionPath, 'server', 'DebugOpsMCP.Host.dll');
        if (fs.existsSync(publishedPath)) {
            return publishedPath;
        }

        return null;
    }

    private setupProcessHandlers(): void {
        if (!this.serverProcess) {
            return;
        }

        this.serverProcess.on('exit', (code, signal) => {
            this.outputChannel.appendLine(`DebugOpsMCP server exited with code: ${code}, signal: ${signal}`);
            this.status.isRunning = false;
            this.serverProcess = undefined;
        });

        this.serverProcess.on('error', (error) => {
            this.outputChannel.appendLine(`DebugOpsMCP server error: ${error.message}`);
            this.status.lastError = error.message;
            this.status.isRunning = false;
        });

        // Log server output for debugging
        this.serverProcess.stdout?.on('data', (data) => {
            // Don't log MCP responses to avoid spam, but log other output
            const output = data.toString();
            if (!output.startsWith('{') && !output.startsWith('[')) {
                this.outputChannel.appendLine(`Server: ${output}`);
            }
        });

        this.serverProcess.stderr?.on('data', (data) => {
            this.outputChannel.appendLine(`Server Error: ${data.toString()}`);
        });
    }

    private async sendHealthCheck(): Promise<void> {
        try {
            const response = await this.sendMcpRequest({ method: 'health' });

            if (response.success) {
                this.outputChannel.appendLine('Health check passed');
            } else {
                throw new Error(`Health check failed: ${response.message}`);
            }
        } catch (error) {
            throw new Error(`Health check failed: ${error}`);
        }
    }
}