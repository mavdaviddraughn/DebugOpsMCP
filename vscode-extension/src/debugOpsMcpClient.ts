import { ChildProcess, spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

export interface DebugBridgeMessage {
    id: string;
    type: 'request' | 'response' | 'event';
    method?: string;
    data?: any;
    error?: string;
    timestamp: string;
}

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
    private pendingRequests = new Map<string, {
        resolve: (value: any) => void;
        reject: (error: Error) => void;
        timeout: NodeJS.Timeout;
    }>();
    private debugSessions = new Map<string, vscode.DebugSession>();

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.outputChannel = vscode.window.createOutputChannel('DebugOpsMCP');
        context.subscriptions.push(this.outputChannel);

        // Set up debug session management
        this.setupDebugSessionHandlers();
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

            // Send initial ping to establish communication
            await this.sendPing();

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

    async sendDebugRequest(method: string, data?: any): Promise<any> {
        if (!this.status.isRunning || !this.serverProcess) {
            throw new Error('DebugOpsMCP server is not running');
        }

        const message: DebugBridgeMessage = {
            id: this.generateId(),
            type: 'response',
            method,
            data,
            timestamp: new Date().toISOString()
        };

        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                this.pendingRequests.delete(message.id);
                reject(new Error(`Request timeout for method: ${method}`));
            }, 30000);

            this.pendingRequests.set(message.id, {
                resolve,
                reject,
                timeout
            });

            // Send the response message to the server
            this.sendMessage(message);
        });
    }

    async handleDebugBridgeRequest(message: DebugBridgeMessage): Promise<void> {
        if (!message.method) {
            this.sendErrorResponse(message.id, 'No method specified');
            return;
        }

        this.outputChannel.appendLine(`Handling debug request: ${message.method}`);

        try {
            let responseData: any = {};

            // Handle different debug operations
            switch (message.method) {
                case 'debug.attach':
                    responseData = await this.handleAttach(message.data);
                    break;
                case 'debug.detach':
                    responseData = await this.handleDetach(message.data);
                    break;
                case 'debug.launch':
                    responseData = await this.handleLaunch(message.data);
                    break;
                case 'debug.setbreakpoint':
                    responseData = await this.handleSetBreakpoint(message.data);
                    break;
                case 'debug.removebreakpoint':
                    responseData = await this.handleRemoveBreakpoint(message.data);
                    break;
                case 'debug.continue':
                    responseData = await this.handleContinue(message.data);
                    break;
                case 'debug.step':
                    responseData = await this.handleStep(message.data);
                    break;
                case 'debug.getvariables':
                    responseData = await this.handleGetVariables(message.data);
                    break;
                case 'debug.getthreads':
                    responseData = await this.handleGetThreads(message.data);
                    break;
                case 'debug.getstackframes':
                    responseData = await this.handleGetStackFrames(message.data);
                    break;
                case 'ping':
                    responseData = { message: 'pong', timestamp: new Date().toISOString() };
                    break;
                default:
                    throw new Error(`Unknown method: ${message.method}`);
            }

            this.sendResponse(message.id, responseData);
        } catch (error) {
            this.sendErrorResponse(message.id, error instanceof Error ? error.message : String(error));
        }
    }

    private async getServerPath(): Promise<string | null> {
        // First, check configuration
        const config = vscode.workspace.getConfiguration('debugops-mcp');
        const configuredPath = config.get<string>('serverPath');

        if (configuredPath && configuredPath.trim() !== '') {
            // If configured path exists, use it
            if (fs.existsSync(configuredPath)) {
                this.outputChannel.appendLine(`Using configured server path: ${configuredPath}`);
                return configuredPath;
            } else {
                this.outputChannel.appendLine(`Warning: Configured server path does not exist: ${configuredPath}`);
            }
        }

        // Auto-detect server path using multiple fallback strategies
        const extensionPath = this.context.extensionPath;
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        
        const candidatePaths: { path: string; description: string }[] = [
            // Development build paths (relative to extension)
            { 
                path: path.resolve(extensionPath, '..', 'core', 'src', 'DebugOpsMCP.Host', 'bin', 'Debug', 'net8.0', 'DebugOpsMCP.Host.dll'),
                description: 'Development Debug build' 
            },
            { 
                path: path.resolve(extensionPath, '..', 'core', 'src', 'DebugOpsMCP.Host', 'bin', 'Release', 'net8.0', 'DebugOpsMCP.Host.dll'),
                description: 'Development Release build' 
            },
            
            // Workspace-relative paths (if opened in workspace)
            ...(workspacePath ? [
                {
                    path: path.join(workspacePath, 'core', 'src', 'DebugOpsMCP.Host', 'bin', 'Debug', 'net8.0', 'DebugOpsMCP.Host.dll'),
                    description: 'Workspace Debug build'
                },
                {
                    path: path.join(workspacePath, 'core', 'src', 'DebugOpsMCP.Host', 'bin', 'Release', 'net8.0', 'DebugOpsMCP.Host.dll'),
                    description: 'Workspace Release build'
                },
            ] : []),
            
            // Published/packaged paths
            { 
                path: path.join(extensionPath, 'server', 'DebugOpsMCP.Host.dll'),
                description: 'Packaged server' 
            },
            { 
                path: path.join(extensionPath, 'DebugOpsMCP.Host.dll'),
                description: 'Extension directory' 
            },
            
            // Global installation paths
            { 
                path: path.join(process.env.APPDATA || '', 'DebugOpsMCP', 'DebugOpsMCP.Host.dll'),
                description: 'Global AppData installation' 
            },
        ];

        // Try each candidate path
        for (const candidate of candidatePaths) {
            if (fs.existsSync(candidate.path)) {
                this.outputChannel.appendLine(`Found server at ${candidate.description}: ${candidate.path}`);
                return candidate.path;
            } else {
                this.outputChannel.appendLine(`Checked ${candidate.description}: ${candidate.path} (not found)`);
            }
        }

        // If no server found, provide helpful error message
        this.outputChannel.appendLine('=== DebugOpsMCP Server Not Found ===');
        this.outputChannel.appendLine('Could not find DebugOpsMCP server. Please either:');
        this.outputChannel.appendLine('1. Build the server: cd core && dotnet build');
        this.outputChannel.appendLine('2. Configure the path manually in VS Code settings: debugops-mcp.serverPath');
        this.outputChannel.appendLine('3. Ensure the server is built in one of these locations:');
        candidatePaths.forEach((candidate, index) => {
            this.outputChannel.appendLine(`   ${index + 1}. ${candidate.path}`);
        });

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

        // Log server output and handle incoming messages
        this.serverProcess.stdout?.on('data', (data) => {
            const output = data.toString();
            const lines = output.split('\n').filter((line: string) => line.trim());
            
            for (const line of lines) {
                try {
                    const message: DebugBridgeMessage = JSON.parse(line);
                    this.handleIncomingMessage(message);
                } catch (error) {
                    // Not a JSON message, log as regular output
                    if (line.trim()) {
                        this.outputChannel.appendLine(`Server: ${line}`);
                    }
                }
            }
        });

        this.serverProcess.stderr?.on('data', (data) => {
            this.outputChannel.appendLine(`Server Error: ${data.toString()}`);
        });
    }

    private async sendPing(): Promise<void> {
        try {
            await this.sendDebugRequest('ping');
            this.outputChannel.appendLine('Server communication established');
        } catch (error) {
            throw new Error(`Failed to establish server communication: ${error}`);
        }
    }

    private handleIncomingMessage(message: DebugBridgeMessage): void {
        if (message.type === 'request') {
            // Server is making a request to us
            this.handleDebugBridgeRequest(message);
        } else if (message.type === 'response') {
            // Server is responding to our request
            const pending = this.pendingRequests.get(message.id);
            if (pending) {
                clearTimeout(pending.timeout);
                this.pendingRequests.delete(message.id);
                
                if (message.error) {
                    pending.reject(new Error(message.error));
                } else {
                    pending.resolve(message.data);
                }
            }
        } else if (message.type === 'event') {
            // Server is sending an event
            this.handleDebugEvent(message);
        }
    }

    private handleDebugEvent(message: DebugBridgeMessage): void {
        this.outputChannel.appendLine(`Debug event: ${message.method}`);
        // Handle debug events from the server
        // This could trigger VS Code UI updates, etc.
    }

    private generateId(): string {
        return Math.random().toString(36).substring(2) + Date.now().toString(36);
    }

    private sendMessage(message: DebugBridgeMessage): void {
        if (!this.serverProcess?.stdin) {
            throw new Error('Server process not available');
        }
        
        const json = JSON.stringify(message);
        this.serverProcess.stdin.write(json + '\n');
    }

    private sendResponse(requestId: string, data: any): void {
        const response: DebugBridgeMessage = {
            id: requestId,
            type: 'response',
            data,
            timestamp: new Date().toISOString()
        };
        
        this.sendMessage(response);
    }

    private sendErrorResponse(requestId: string, error: string): void {
        const response: DebugBridgeMessage = {
            id: requestId,
            type: 'response',
            error,
            timestamp: new Date().toISOString()
        };
        
        this.sendMessage(response);
    }

    // Debug operation handlers - these interface with VS Code's Debug API
    private async handleAttach(data: any): Promise<any> {
        try {
            if (!data?.processId) {
                throw new Error('processId is required for attach operation');
            }

            // Create debug configuration for attach
            const debugConfig: vscode.DebugConfiguration = {
                name: 'DebugOpsMCP Attach',
                type: data.debuggerType || 'node', // Default to node, but should be specified
                request: 'attach',
                processId: data.processId,
                ...(data.configuration || {})
            };

            // Start debug session
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            const success = await vscode.debug.startDebugging(workspaceFolder, debugConfig);
            
            if (success) {
                const activeSession = vscode.debug.activeDebugSession;
                return {
                    success: true,
                    sessionId: activeSession?.id || 'unknown',
                    processId: data.processId,
                    capabilities: this.getDebugCapabilities(activeSession)
                };
            } else {
                throw new Error('Failed to start debug session');
            }
        } catch (error) {
            this.outputChannel.appendLine(`Attach failed: ${error}`);
            throw error;
        }
    }

    private async handleDetach(data: any): Promise<any> {
        try {
            const sessionId = data?.sessionId;
            let targetSession: vscode.DebugSession | undefined;
            
            if (sessionId) {
                // Find specific session by ID
                targetSession = this.debugSessions.get(sessionId);
                if (!targetSession) {
                    throw new Error(`Debug session ${sessionId} not found`);
                }
            } else {
                // Use active debug session
                targetSession = vscode.debug.activeDebugSession;
                if (!targetSession) {
                    throw new Error('No active debug session to detach from');
                }
            }

            // Stop the debug session
            await vscode.debug.stopDebugging(targetSession);
            
            return {
                success: true,
                sessionId: targetSession.id,
                message: 'Debug session detached successfully'
            };
        } catch (error) {
            this.outputChannel.appendLine(`Detach failed: ${error}`);
            throw error;
        }
    }

    private async handleLaunch(data: any): Promise<any> {
        try {
            if (!data?.program) {
                throw new Error('program is required for launch operation');
            }

            // Create debug configuration for launch
            const debugConfig: vscode.DebugConfiguration = {
                name: 'DebugOpsMCP Launch',
                type: data.debuggerType || 'node', // Should be specified based on program type
                request: 'launch',
                program: data.program,
                args: data.args || [],
                cwd: data.cwd || vscode.workspace.workspaceFolders?.[0]?.uri.fsPath,
                ...(data.configuration || {})
            };

            // Start debug session
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            const success = await vscode.debug.startDebugging(workspaceFolder, debugConfig);
            
            if (success) {
                const activeSession = vscode.debug.activeDebugSession;
                return {
                    success: true,
                    sessionId: activeSession?.id || 'unknown',
                    program: data.program,
                    capabilities: this.getDebugCapabilities(activeSession)
                };
            } else {
                throw new Error('Failed to start debug session');
            }
        } catch (error) {
            this.outputChannel.appendLine(`Launch failed: ${error}`);
            throw error;
        }
    }

    private async handleSetBreakpoint(data: any): Promise<any> {
        try {
            if (!data?.file || data?.line === undefined) {
                throw new Error('file and line are required for breakpoint operations');
            }

            const uri = vscode.Uri.file(data.file);
            const line = parseInt(data.line) - 1; // VS Code uses 0-based line numbers
            
            // Create breakpoint
            const location = new vscode.Location(uri, new vscode.Position(line, 0));
            const breakpoint = new vscode.SourceBreakpoint(location, true);
            
            // Add breakpoint to VS Code
            const existingBreakpoints = vscode.debug.breakpoints;
            vscode.debug.addBreakpoints([breakpoint]);
            
            return {
                success: true,
                id: `bp_${Date.now()}`, // Generate unique ID
                file: data.file,
                line: data.line,
                verified: true,
                condition: data.condition,
                hitCondition: data.hitCondition
            };
        } catch (error) {
            this.outputChannel.appendLine(`Set breakpoint failed: ${error}`);
            throw error;
        }
    }

    private async handleRemoveBreakpoint(data: any): Promise<any> {
        try {
            if (!data?.file || data?.line === undefined) {
                throw new Error('file and line are required for breakpoint removal');
            }

            const uri = vscode.Uri.file(data.file);
            const line = parseInt(data.line) - 1; // VS Code uses 0-based line numbers
            
            // Find and remove matching breakpoints
            const existingBreakpoints = vscode.debug.breakpoints;
            const toRemove = existingBreakpoints.filter(bp => {
                if (bp instanceof vscode.SourceBreakpoint) {
                    return bp.location.uri.fsPath === uri.fsPath && 
                           bp.location.range.start.line === line;
                }
                return false;
            });
            
            if (toRemove.length > 0) {
                vscode.debug.removeBreakpoints(toRemove);
                return {
                    success: true,
                    message: `Removed ${toRemove.length} breakpoint(s) at ${data.file}:${data.line}`
                };
            } else {
                return {
                    success: false,
                    message: `No breakpoint found at ${data.file}:${data.line}`
                };
            }
        } catch (error) {
            this.outputChannel.appendLine(`Remove breakpoint failed: ${error}`);
            throw error;
        }
    }

    private async handleContinue(data: any): Promise<any> {
        try {
            const session = this.getTargetDebugSession(data?.sessionId);
            if (!session) {
                throw new Error('No active debug session for continue operation');
            }

            // Send continue request to debug adapter
            await session.customRequest('continue', {
                threadId: data?.threadId || 1 // Default to thread 1 if not specified
            });
            
            return {
                success: true,
                sessionId: session.id,
                message: 'Continue execution requested'
            };
        } catch (error) {
            this.outputChannel.appendLine(`Continue failed: ${error}`);
            throw error;
        }
    }

    private async handleStep(data: any): Promise<any> {
        try {
            const session = this.getTargetDebugSession(data?.sessionId);
            if (!session) {
                throw new Error('No active debug session for step operation');
            }

            const stepType = data?.stepType || 'over';
            const threadId = data?.threadId || 1;
            
            // Map step types to DAP commands
            let command: string;
            switch (stepType.toLowerCase()) {
                case 'into':
                case 'stepin':
                    command = 'stepIn';
                    break;
                case 'out':
                case 'stepout':
                    command = 'stepOut';
                    break;
                case 'over':
                case 'stepover':
                default:
                    command = 'next';
                    break;
            }

            // Send step request to debug adapter
            await session.customRequest(command, { threadId });
            
            return {
                success: true,
                sessionId: session.id,
                stepType,
                message: `Step ${stepType} requested`
            };
        } catch (error) {
            this.outputChannel.appendLine(`Step failed: ${error}`);
            throw error;
        }
    }

    private async handleGetVariables(data: any): Promise<any> {
        try {
            const session = this.getTargetDebugSession(data?.sessionId);
            if (!session) {
                throw new Error('No active debug session for variables operation');
            }

            const variablesReference = data?.variablesReference || 0;
            
            // Send variables request to debug adapter
            const response = await session.customRequest('variables', {
                variablesReference
            });
            
            return {
                success: true,
                variables: response?.body?.variables || [],
                sessionId: session.id
            };
        } catch (error) {
            this.outputChannel.appendLine(`Get variables failed: ${error}`);
            // Return empty array on failure
            return {
                success: false,
                variables: [],
                error: error instanceof Error ? error.message : String(error)
            };
        }
    }

    private async handleGetThreads(data: any): Promise<any> {
        try {
            const session = this.getTargetDebugSession(data?.sessionId);
            if (!session) {
                throw new Error('No active debug session for threads operation');
            }
            
            // Send threads request to debug adapter
            const response = await session.customRequest('threads');
            
            return {
                success: true,
                threads: response?.body?.threads || [],
                sessionId: session.id
            };
        } catch (error) {
            this.outputChannel.appendLine(`Get threads failed: ${error}`);
            // Return empty array on failure
            return {
                success: false,
                threads: [],
                error: error instanceof Error ? error.message : String(error)
            };
        }
    }

    private async handleGetStackFrames(data: any): Promise<any> {
        try {
            const session = this.getTargetDebugSession(data?.sessionId);
            if (!session) {
                throw new Error('No active debug session for stack frames operation');
            }

            const threadId = data?.threadId || 1;
            const startFrame = data?.startFrame || 0;
            const levels = data?.levels || 20;
            
            // Send stackTrace request to debug adapter
            const response = await session.customRequest('stackTrace', {
                threadId,
                startFrame,
                levels
            });
            
            return {
                success: true,
                stackFrames: response?.body?.stackFrames || [],
                totalFrames: response?.body?.totalFrames,
                sessionId: session.id,
                threadId
            };
        } catch (error) {
            this.outputChannel.appendLine(`Get stack frames failed: ${error}`);
            // Return empty array on failure
            return {
                success: false,
                stackFrames: [],
                error: error instanceof Error ? error.message : String(error)
            };
        }
    }

    private setupDebugSessionHandlers(): void {
        // Set up VS Code debug session event handlers
        vscode.debug.onDidStartDebugSession((session) => {
            this.debugSessions.set(session.id, session);
            this.outputChannel.appendLine(`Debug session started: ${session.name} (${session.type})`);
            
            // Notify the server about the debug session
            this.sendDebugEvent('debugSessionStarted', {
                sessionId: session.id,
                sessionName: session.name,
                sessionType: session.type
            });
        });

        vscode.debug.onDidTerminateDebugSession((session) => {
            this.debugSessions.delete(session.id);
            this.outputChannel.appendLine(`Debug session terminated: ${session.name}`);
            
            // Notify the server about the session termination
            this.sendDebugEvent('debugSessionTerminated', {
                sessionId: session.id,
                sessionName: session.name
            });
        });
    }

    private sendDebugEvent(method: string, data: any): void {
        if (!this.status.isRunning) {
            return;
        }
        
        const event: DebugBridgeMessage = {
            id: this.generateId(),
            type: 'event',
            method,
            data,
            timestamp: new Date().toISOString()
        };
        
        try {
            this.sendMessage(event);
        } catch (error) {
            this.outputChannel.appendLine(`Failed to send debug event: ${error}`);
        }
    }

    private getTargetDebugSession(sessionId?: string): vscode.DebugSession | undefined {
        if (sessionId) {
            // Find specific session by ID
            return this.debugSessions.get(sessionId) || 
                   [...this.debugSessions.values()].find(s => s.id === sessionId);
        } else {
            // Use active debug session
            return vscode.debug.activeDebugSession;
        }
    }

    private getDebugCapabilities(session?: vscode.DebugSession): any {
        // Return standard capabilities - in a real implementation, these would be
        // retrieved from the actual debug adapter's initialize response
        return {
            supportsBreakpoints: true,
            supportsConditionalBreakpoints: true,
            supportsEvaluateForHovers: true,
            supportsStepBack: false,
            supportsSetVariable: true,
            supportsConfigurationDoneRequest: true,
            supportsHitConditionalBreakpoints: true,
            supportsFunctionBreakpoints: false,
            supportsRestartRequest: false,
            supportsExceptionOptions: true,
            supportsValueFormattingOptions: true,
            supportTerminateDebuggee: true,
            supportSuspendDebuggee: true,
            supportsDelayedStackTraceLoading: true,
            supportsLoadedSourcesRequest: false,
            supportsLogPoints: true,
            supportsTerminateThreadsRequest: false,
            supportsSetExpression: false,
            supportsTerminateRequest: true,
            supportsDataBreakpoints: false,
            supportsReadMemoryRequest: false,
            supportsWriteMemoryRequest: false,
            supportsDisassembleRequest: false,
            supportsCancelRequest: true,
            supportsBreakpointLocationsRequest: false,
            supportsSteppingGranularity: false,
            supportsInstructionBreakpoints: false,
            supportsExceptionInfoRequest: true
        };
    }
}