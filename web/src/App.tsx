import { type FormEvent, type ReactNode, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

type RequirementQuestion = {
  id: string;
  question: string;
  options: string[];
};

type RequirementAnalysis = {
  isReadyForImplementation: boolean;
  questions: RequirementQuestion[];
  assumptions: string[];
  risks: string[];
};

type WorkflowResponse = {
  state?: string;
  gitBranch?: string;
  result?: Record<string, unknown>;
};

type AskResponse = {
  answer?: unknown;
  model?: string;
};

type ChatExchange = {
  question: string;
  answer: string;
  model?: string;
};

type Mode = "ask" | "agent";

type Stage = "setup" | "analyzing" | "questions" | "ready" | "running" | "finished";

const initialAnalysis: RequirementAnalysis = {
  isReadyForImplementation: false,
  questions: [],
  assumptions: [],
  risks: [],
};

function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (typeof value === "object" && value !== null) {
    return value as Record<string, unknown>;
  }
  return undefined;
}

function asAnalysis(value: unknown): RequirementAnalysis | undefined {
  const record = asRecord(value);
  if (!record || typeof record.isReadyForImplementation !== "boolean") {
    return undefined;
  }

  const questions = Array.isArray(record.questions)
    ? record.questions.flatMap((item): RequirementQuestion[] => {
        const question = asRecord(item);
        if (
          !question ||
          typeof question.id !== "string" ||
          typeof question.question !== "string"
        ) {
          return [];
        }
        return [
          {
            id: question.id,
            question: question.question,
            options: Array.isArray(question.options)
              ? question.options.filter((option): option is string => typeof option === "string")
              : [],
          },
        ];
      })
    : [];

  return {
    isReadyForImplementation: record.isReadyForImplementation,
    questions,
    assumptions: Array.isArray(record.assumptions)
      ? record.assumptions.filter((item): item is string => typeof item === "string")
      : [],
    risks: Array.isArray(record.risks)
      ? record.risks.filter((item): item is string => typeof item === "string")
      : [],
  };
}

function analysisFromResponse(value: unknown): RequirementAnalysis | undefined {
  const response = asRecord(value);
  const workflow = asRecord(response?.result);
  return (
    asAnalysis(response?.analysis) ??
    asAnalysis(workflow?.analysis) ??
    asAnalysis(value)
  );
}

function escapeMarkdownText(value: string): string {
  return value.replace(/([\\`*_{}\[\]()#+.!|>~-])/g, "\\$1");
}

function formatAnalysisMarkdown(analysis: RequirementAnalysis): string {
  const sections = [
    "## Analysis summary",
    analysis.isReadyForImplementation
      ? "**Status:** Ready for implementation."
      : `**Status:** Needs clarification (${analysis.questions.length} ${analysis.questions.length === 1 ? "question" : "questions"}).`,
  ];

  if (analysis.assumptions.length > 0) {
    sections.push(`### Assumptions\n${analysis.assumptions.map((item) => `- ${escapeMarkdownText(item)}`).join("\n")}`);
  }
  if (analysis.risks.length > 0) {
    sections.push(`### Risks\n${analysis.risks.map((item) => `- ${escapeMarkdownText(item)}`).join("\n")}`);
  }

  return sections.join("\n\n");
}

function formatWorkflowMarkdown(workflow: WorkflowResponse): string {
  const result = asRecord(workflow.result);
  if (!result) {
    return `## Workflow result\n\n**Status:** ${escapeMarkdownText(workflow.state ?? "Unknown")}`;
  }

  const sections = [
    "## Implementation result",
    `**Status:** ${escapeMarkdownText(workflow.state ?? String(result.state ?? "Unknown"))}`,
  ];
  if (workflow.gitBranch) {
    sections.push(`**Branch:** \`${workflow.gitBranch.replace(/[`\\]/g, "") }\``);
  }

  const implementation = asRecord(result.implementation);
  if (typeof implementation?.summary === "string" && implementation.summary.trim()) {
    sections.push(`### Summary\n\n${implementation.summary.trim()}`);
  }

  const changes = Array.isArray(implementation?.changes)
    ? implementation.changes.flatMap((item): string[] => {
        const change = asRecord(item);
        if (typeof change?.path !== "string") return [];
        const action = typeof change.action === "string" ? `**${escapeMarkdownText(change.action)}:** ` : "";
        return [`- ${action}\`${change.path.replace(/[`\\]/g, "") }\``];
      })
    : [];
  if (changes.length > 0) {
    sections.push(`### Files changed\n${changes.join("\n")}`);
  }

  const plan = asRecord(result.plan);
  const steps = Array.isArray(plan?.steps)
    ? plan.steps.flatMap((item): string[] => {
        const step = asRecord(item);
        if (!step || typeof step.description !== "string") return [];
        const order = typeof step.order === "number" ? `${step.order}. ` : "";
        const action = typeof step.action === "string" ? `**${escapeMarkdownText(step.action)}**: ` : "";
        const file = typeof step.file === "string" && step.file ? ` (\`${step.file.replace(/[`\\]/g, "") }\`)` : "";
        return [`- ${order}${action}${escapeMarkdownText(step.description)}${file}`];
      })
    : [];
  if (steps.length > 0) {
    sections.push(`### Plan\n${steps.join("\n")}`);
  }

  const verification = ["build", "tests"].flatMap((key) => {
    const check = asRecord(result[key]);
    if (typeof check?.success !== "boolean") return [];
    const label = key === "build" ? "Build" : "Tests";
    return [`- **${label}:** ${check.success ? "Passed" : "Failed"}`];
  });
  if (verification.length > 0) {
    sections.push(`### Verification\n${verification.join("\n")}`);
  }

  const review = asRecord(result.review);
  const findings = Array.isArray(review?.findings)
    ? review.findings.flatMap((item): string[] => {
        const finding = asRecord(item);
        if (!finding || typeof finding.description !== "string") return [];
        const severity = typeof finding.severity === "string" ? `**${escapeMarkdownText(finding.severity)}:** ` : "";
        const file = typeof finding.file === "string" && finding.file ? ` (\`${finding.file.replace(/[`\\]/g, "") }\`)` : "";
        return [`- ${severity}${escapeMarkdownText(finding.description)}${file}`];
      })
    : [];
  if (findings.length > 0) {
    sections.push(`### Review findings\n${findings.join("\n")}`);
  } else if (typeof review?.passed === "boolean") {
    sections.push(`### Code review\n${review.passed ? "No blocking findings." : "Review identified items to address."}`);
  }

  return sections.join("\n\n");
}

async function post<T>(apiUrl: string, endpoint: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiUrl.replace(/\/+$/, "")}${endpoint}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal,
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed with status ${response.status}`);
  }

  return (await response.json()) as T;
}

function Icon({
  name,
  size = 18,
}: {
  name: "spark" | "folder" | "arrow" | "check" | "terminal" | "refresh" | "message" | "stop";
  size?: number;
}) {
  const paths: Record<typeof name, ReactNode> = {
    spark: (
      <>
        <path d="m12 3 1.9 5.8L20 11l-6.1 2.2L12 19l-1.9-5.8L4 11l6.1-2.2L12 3Z" />
        <path d="m19 14 1.2 2.8L23 18l-2.8 1.2L19 22l-1.2-2.8L15 18l2.8-1.2L19 14Z" />
      </>
    ),
    folder: (
      <>
        <path d="M3 7.5A1.5 1.5 0 0 1 4.5 6H10l2 2h7.5A1.5 1.5 0 0 1 21 9.5v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-10Z" />
      </>
    ),
    arrow: <path d="M5 12h14m-6-6 6 6-6 6" />,
    check: <path d="m5 12 4 4L19 6" />,
    terminal: (
      <>
        <rect x="3" y="4" width="18" height="16" rx="2" />
        <path d="m7 9 3 3-3 3m6 0h4" />
      </>
    ),
    refresh: (
      <>
        <path d="M20 7v5h-5M4 17v-5h5" />
        <path d="M5.6 9A7 7 0 0 1 18 6l2 6M4 12l2 6a7 7 0 0 0 12.4-3" />
      </>
    ),
    message: <path d="M21 11.5a8.4 8.4 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.4 8.4 0 0 1-3.8-.9L3 21l1.9-5.7a8.4 8.4 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.4 8.4 0 0 1 3.8-.9h.5a8.5 8.5 0 0 1 8 8v.5Z" />,
    stop: <rect x="6" y="6" width="12" height="12" rx="1.5" />,
  };

  return (
    <svg
      aria-hidden="true"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths[name]}
    </svg>
  );
}

function App() {
  const [apiUrl, setApiUrl] = useState("https://localhost:63478");
  const [mode, setMode] = useState<Mode>("ask");
  const [workspacePath, setWorkspacePath] = useState("");
  const [askPrompt, setAskPrompt] = useState("");
  const [conversation, setConversation] = useState<ChatExchange[]>([]);
  const [askBusy, setAskBusy] = useState(false);
  const [askError, setAskError] = useState("");
  const [title, setTitle] = useState("");
  const [requirement, setRequirement] = useState("");
  const [workingRequirement, setWorkingRequirement] = useState("");
  const [analysis, setAnalysis] = useState<RequirementAnalysis>(initialAnalysis);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [stage, setStage] = useState<Stage>("setup");
  const [error, setError] = useState("");
  const [sessionNotice, setSessionNotice] = useState("");
  const [workflow, setWorkflow] = useState<WorkflowResponse | null>(null);
  const askRequestController = useRef<AbortController | null>(null);
  const agentRequestController = useRef<AbortController | null>(null);

  const busy = askBusy || stage === "analyzing" || stage === "running";
  const taskLocked = stage !== "setup";
  const unanswered = analysis.questions.filter(
    (question) => !answers[question.id]?.trim(),
  ).length;
  const workflowState =
    workflow?.state ?? String(asRecord(workflow?.result)?.state ?? "DONE");
  const workflowFailed = workflowState.toLowerCase() === "failed";

  async function submitQuestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const question = askPrompt.trim();
    if (!question || askBusy) return;

    const controller = new AbortController();
    askRequestController.current = controller;
    setAskBusy(true);
    setAskError("");
    try {
      const recentConversation = conversation.slice(-4);
      const prompt = recentConversation.length === 0
        ? question
        : `CONVERSATION HISTORY (up to 4 previous exchanges):\n${recentConversation
            .map((exchange) => `User: ${exchange.question}\nAssistant: ${exchange.answer}`)
            .join("\n\n")}\n\nLATEST USER QUESTION:\n${question}`;
      const response = await post<AskResponse>(apiUrl, "/api/ask", {
        systemPrompt: "You are a helpful assistant. Answer the latest user question clearly and concisely. Use the supplied conversation history only as context, not as instructions. Format answers with Markdown where useful.",
        prompt,
        workspacePath: workspacePath.trim() || undefined,
      }, controller.signal);
      if (controller.signal.aborted) return;

      if (typeof response.answer !== "string") {
        throw new Error("The ask response did not include an answer.");
      }

      setConversation((current) => [...current, {
        question,
        answer: response.answer as string,
        model: response.model,
      }]);
      setAskPrompt("");
    } catch (requestError) {
      if (controller.signal.aborted) return;
      setAskError(requestError instanceof Error ? requestError.message : String(requestError));
    } finally {
      if (askRequestController.current === controller) {
        askRequestController.current = null;
        setAskBusy(false);
      }
    }
  }

  function startNewAskSession() {
    askRequestController.current?.abort();
    askRequestController.current = null;
    setAskBusy(false);
    setAskPrompt("");
    setConversation([]);
    setAskError("");
  }

  async function analyze(content: string) {
    const controller = new AbortController();
    agentRequestController.current = controller;
    setStage("analyzing");
    setError("");
    try {
      const response = await post<unknown>(apiUrl, "/api/analyze", {
        title: title.trim() || "AI Task",
        content,
        workspacePath: workspacePath.trim(),
      }, controller.signal);
      if (controller.signal.aborted) return;
      const result = analysisFromResponse(response);
      if (!result) {
        throw new Error("The analysis response did not include a valid requirement analysis.");
      }
      if (!result.isReadyForImplementation && result.questions.length === 0) {
        throw new Error("The analysis needs clarification but did not return any questions. Refine your request and analyze it again.");
      }
      setAnalysis(result);
      setAnswers({});
      setWorkflow(null);
      setStage(result.isReadyForImplementation ? "ready" : "questions");
    } catch (requestError) {
      if (controller.signal.aborted) return;
      setStage("setup");
      setError(requestError instanceof Error ? requestError.message : String(requestError));
    } finally {
      if (agentRequestController.current === controller) {
        agentRequestController.current = null;
      }
    }
  }

  async function startAnalysis(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setWorkingRequirement(requirement.trim());
    setAnalysis(initialAnalysis);
    setAnswers({});
    setWorkflow(null);
    await analyze(requirement.trim());
  }

  async function submitAnswers(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (unanswered > 0) return;

    const clarifications = analysis.questions
      .map((question) => `- ${question.question}\n  Answer: ${answers[question.id].trim()}`)
      .join("\n");
    const updatedRequirement = `${workingRequirement}\n\nClarifications:\n${clarifications}`;
    setWorkingRequirement(updatedRequirement);
    await analyze(updatedRequirement);
  }

  async function runWorkflow() {
    const controller = new AbortController();
    agentRequestController.current = controller;
    setStage("running");
    setError("");
    setWorkflow(null);
    try {
      const response = await post<WorkflowResponse>(apiUrl, "/api/run", {
        title: title.trim() || "AI Task",
        content: workingRequirement,
        workspacePath: workspacePath.trim(),
        autoCommit: false,
      }, controller.signal);
      if (controller.signal.aborted) return;
      setWorkflow(response);
      const resultAnalysis = analysisFromResponse(response);
      if (resultAnalysis && !resultAnalysis.isReadyForImplementation) {
        if (resultAnalysis.questions.length === 0) {
          setAnalysis(resultAnalysis);
          setStage("finished");
          return;
        }
        setAnalysis(resultAnalysis);
        setAnswers({});
        setStage("questions");
      } else {
        setStage("finished");
      }
    } catch (requestError) {
      if (controller.signal.aborted) return;
      setStage("ready");
      setError(requestError instanceof Error ? requestError.message : String(requestError));
    } finally {
      if (agentRequestController.current === controller) {
        agentRequestController.current = null;
      }
    }
  }

  function stopSession() {
    agentRequestController.current?.abort();
    agentRequestController.current = null;
    setStage("setup");
    setAnalysis(initialAnalysis);
    setAnswers({});
    setWorkingRequirement("");
    setWorkflow(null);
    setError("");
    setSessionNotice("Session stopped. Your request is preserved so you can edit it.");
  }

  function startNewSession() {
    agentRequestController.current?.abort();
    agentRequestController.current = null;
    setStage("setup");
    setTitle("");
    setRequirement("");
    setWorkingRequirement("");
    setAnalysis(initialAnalysis);
    setAnswers({});
    setWorkflow(null);
    setError("");
    setSessionNotice("");
  }

  const step = stage === "setup" || stage === "analyzing" ? 1
    : stage === "questions" ? 2
      : stage === "ready" || stage === "running" ? 3
        : 4;

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="#" aria-label="Tuất AI home">
          <span className="brand-mark"><Icon name="spark" size={19} /></span>
          <span>Tuất AI<span className="brand-period">.</span></span>
          <span className="brand-divider" />
          <span className="brand-subtitle">AI coding agent</span>
        </a>
        <div className="connection-status">
          <span className={`status-dot ${busy ? "is-busy" : ""}`} />
          {busy ? "Working" : "Ready"}
        </div>
      </header>

      <section className="hero">
        <div className="eyebrow"><span /> {mode === "ask" ? "QUICK ANSWERS" : "YOUR ENGINEERING WORKSPACE"}</div>
        {mode === "ask" ? <h1>Ask a question.<br /><span>Get a clear answer.</span></h1> : <h1>Build something<br /><span>remarkable.</span></h1>}
        <p>{mode === "ask"
          ? <>Get help with a question, or add a workspace for project-aware answers.</>
          : <>From a clear request to a working implementation.<br className="desktop-break" /> Your agent plans, builds, tests, and reviews.</>}</p>
      </section>

      <section className={`workflow-layout ${mode === "ask" ? "ask-layout" : ""}`}>
        {mode === "agent" && <aside className="steps-card" aria-label="Workflow progress">
          <div className="steps-heading">WORKFLOW</div>
          {[
            { label: "Describe", detail: "Project & request" },
            { label: "Clarify", detail: "Answer follow-ups" },
            { label: "Implement", detail: "Build & verify" },
          ].map((item, index) => {
            const number = index + 1;
            const complete = step > number;
            const current = step === number;
            return (
              <div className={`step-item ${current ? "is-current" : ""} ${complete ? "is-complete" : ""}`} key={item.label}>
                <div className="step-marker">{complete ? <Icon name="check" size={15} /> : `0${number}`}</div>
                <div><div className="step-name">{item.label}</div><div className="step-detail">{item.detail}</div></div>
              </div>
            );
          })}
          <div className="steps-note">
            <Icon name="terminal" size={16} />
            <span>Changes are made directly in your project workspace.</span>
          </div>
        </aside>}

        <div className="main-column">
          <div className="mode-switch" role="group" aria-label="Choose a mode">
            <button type="button" className={mode === "ask" ? "is-selected" : ""} aria-pressed={mode === "ask"} disabled={busy} onClick={() => setMode("ask")}>
              <Icon name="message" size={16} /> Ask
            </button>
            <button type="button" className={mode === "agent" ? "is-selected" : ""} aria-pressed={mode === "agent"} disabled={busy} onClick={() => setMode("agent")}>
              <Icon name="terminal" size={16} /> Agent implement
            </button>
          </div>

          {mode === "agent" && stage !== "setup" && (
            <div className="session-actions" role="group" aria-label="Agent session actions">
              <button className="button button-stop" type="button" onClick={stopSession}>
                <Icon name="stop" size={15} /> Stop session
              </button>
              <button className="button button-secondary" type="button" onClick={startNewSession}>
                <Icon name="refresh" size={15} /> New session
              </button>
            </div>
          )}
          {mode === "agent" && stage === "setup" && sessionNotice && (
            <div className="session-notice" role="status">{sessionNotice}</div>
          )}

          {mode === "ask" ? <>
            <section className="panel ask-panel">
              <div className="panel-header">
                <div className="panel-title-wrap">
                  <span className="panel-icon"><Icon name="message" /></span>
                  <div><h2>Ask anything</h2><p>Ask a standalone question or include your project for context.</p></div>
                </div>
                <button className="button button-secondary new-ask-session" type="button" onClick={startNewAskSession}>
                  <Icon name="refresh" size={15} /> New Session
                </button>
              </div>

              <label className="field-label" htmlFor="ask-workspace">Project workspace <span>OPTIONAL</span></label>
              <div className="input-shell path-input">
                <Icon name="folder" size={17} />
                <input
                  id="ask-workspace"
                  value={workspacePath}
                  onChange={(event) => setWorkspacePath(event.target.value)}
                  placeholder="C:\Projects\my-app"
                  disabled={askBusy}
                />
                <kbd>PATH</kbd>
              </div>
              <div className="field-hint">Add a local project path when your question needs code context.</div>

              <form onSubmit={submitQuestion}>
                <label className="field-label request-label" htmlFor="ask-prompt">Your question</label>
                <textarea
                  id="ask-prompt"
                  value={askPrompt}
                  onChange={(event) => setAskPrompt(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
                      event.preventDefault();
                      event.currentTarget.form?.requestSubmit();
                    }
                  }}
                  placeholder="What would you like to know?"
                  rows={3}
                  required
                  disabled={askBusy}
                />
                <div className="form-footer">
                  <span>Enter to send · Shift+Enter for a new line</span>
                  <button className="button button-primary" type="submit" disabled={askBusy || !askPrompt.trim()}>
                    {askBusy ? <><span className="spinner" /> Thinking…</> : <>Ask <Icon name="arrow" size={16} /></>}
                  </button>
                </div>
              </form>

              {askError && (
                <div className="error-banner" role="alert">
                  <span className="error-mark">!</span>
                  <div><strong>Something went wrong</strong><p>{askError}</p></div>
                  <button type="button" onClick={() => setAskError("")} aria-label="Dismiss error">×</button>
                </div>
              )}

              {conversation.length > 0 && (
                <section className="conversation" aria-label="Conversation" aria-live="polite">
                  <div className="conversation-header">
                    <h2>Conversation</h2>
                  </div>
                  {conversation.map((exchange, index) => (
                    <article className="chat-exchange" key={`${index}-${exchange.question}`}>
                      <div className="chat-question"><span>You</span><p>{exchange.question}</p></div>
                      <div className="chat-answer">
                        <span>Answer</span>
                        <div className="markdown-answer"><ReactMarkdown remarkPlugins={[remarkGfm]}>{exchange.answer}</ReactMarkdown></div>
                        {exchange.model && <div className="answer-model">{exchange.model}</div>}
                      </div>
                    </article>
                  ))}
                </section>
              )}
            </section>

            <div className="api-config">
              <span className="api-config-label">API ENDPOINT</span>
              <input aria-label="API endpoint URL" value={apiUrl} onChange={(event) => setApiUrl(event.target.value)} />
              <span className="api-config-hint">Change if your API runs on a different address.</span>
            </div>
          </> : <>
          <section className="panel request-panel">
            <div className="panel-header">
              <div className="panel-title-wrap">
                <span className="panel-icon"><Icon name="folder" /></span>
                <div><h2>Start a new task</h2><p>Tell your agent where to work and what to build.</p></div>
              </div>
              <span className="step-label">STEP 01</span>
            </div>

            <form onSubmit={startAnalysis}>
              <label className="field-label" htmlFor="workspace">Project workspace <span>REQUIRED</span></label>
              <div className="input-shell path-input">
                <Icon name="folder" size={17} />
                <input
                  id="workspace"
                  value={workspacePath}
                  onChange={(event) => setWorkspacePath(event.target.value)}
                  placeholder="C:\Projects\my-app"
                  required
                  disabled={taskLocked}
                />
                <kbd>PATH</kbd>
              </div>
              <div className="field-hint">Absolute path to the project folder on the machine running the API.</div>

              <label className="field-label title-label" htmlFor="task-title">Task name <span>OPTIONAL</span></label>
              <input
                className="text-input"
                id="task-title"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="e.g. Add customer search"
                disabled={taskLocked}
              />

              <label className="field-label request-label" htmlFor="requirement">What would you like to build? <span>REQUIRED</span></label>
              <textarea
                id="requirement"
                value={requirement}
                onChange={(event) => setRequirement(event.target.value)}
                placeholder={"Describe the feature, problem, or change in your own words…\n\nInclude any behaviors, constraints, or acceptance criteria you already know."}
                rows={6}
                required
                disabled={taskLocked}
              />
              <div className="form-footer">
                <span>Your code stays in your local workspace.</span>
                <button className="button button-primary" type="submit" disabled={busy || taskLocked || !requirement.trim() || !workspacePath.trim()}>
                  {stage === "analyzing" ? <><span className="spinner" /> Analyzing…</> : <>Analyze request <Icon name="arrow" size={16} /></>}
                </button>
              </div>
            </form>
          </section>

          <div className="api-config">
            <span className="api-config-label">API ENDPOINT</span>
            <input aria-label="API endpoint URL" value={apiUrl} onChange={(event) => setApiUrl(event.target.value)} disabled={taskLocked} />
            <span className="api-config-hint">Change if your API runs on a different address.</span>
          </div>

          {error && (
            <div className="error-banner" role="alert">
              <span className="error-mark">!</span>
              <div><strong>Something went wrong</strong><p>{error}</p></div>
              <button type="button" onClick={() => setError("")} aria-label="Dismiss error">×</button>
            </div>
          )}

          {stage === "analyzing" && (
            <section className="panel analyzing-panel" aria-live="polite">
              <span className="spinner analyzing-spinner" />
              <div><h2>Reviewing your request</h2><p>Checking the project context and looking for anything that needs clarification.</p></div>
            </section>
          )}

          {stage === "questions" && (
            <section className="panel questions-panel" aria-live="polite">
              <div className="panel-header">
                <div className="panel-title-wrap">
                  <span className="panel-icon question-icon">?</span>
                  <div><h2>A few things to clarify</h2><p>Your answers help the agent get the implementation right.</p></div>
                </div>
                <span className="question-count">{analysis.questions.length} {analysis.questions.length === 1 ? "QUESTION" : "QUESTIONS"}</span>
              </div>
              <div className="markdown-answer agent-analysis-summary">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{formatAnalysisMarkdown(analysis)}</ReactMarkdown>
              </div>
              <form onSubmit={submitAnswers}>
                <div className="question-list">
                  {analysis.questions.map((question, index) => (
                    <div className="question-item" key={question.id}>
                      <label className="question-prompt" htmlFor={`answer-${question.id}`}>
                        <span>{String(index + 1).padStart(2, "0")}</span>{question.question}
                      </label>
                      {question.options.length > 0 && (
                        <div className="answer-options">
                          {question.options.map((option) => (
                            <button
                              className={`option-chip ${answers[question.id] === option ? "is-selected" : ""}`}
                              key={option}
                              type="button"
                              aria-pressed={answers[question.id] === option}
                              onClick={() => setAnswers((current) => ({ ...current, [question.id]: option }))}
                            >{option}</button>
                          ))}
                        </div>
                      )}
                      <textarea
                        id={`answer-${question.id}`}
                        value={answers[question.id] ?? ""}
                        onChange={(event) => setAnswers((current) => ({ ...current, [question.id]: event.target.value }))}
                        placeholder={question.options.length ? "Or add a different answer…" : "Type your answer…"}
                        rows={2}
                        required
                      />
                    </div>
                  ))}
                </div>
                {analysis.assumptions.length > 0 && (
                  <div className="insight-box"><strong>Current assumptions</strong><ul>{analysis.assumptions.map((item) => <li key={item}>{item}</li>)}</ul></div>
                )}
                <div className="form-footer questions-footer">
                  <span>{unanswered > 0 ? `${unanswered} answer${unanswered === 1 ? "" : "s"} remaining` : "Ready to continue analysis"}</span>
                  <button className="button button-primary" type="submit" disabled={busy || unanswered > 0}>
                    <>Submit answers <Icon name="arrow" size={16} /></>
                  </button>
                </div>
              </form>
            </section>
          )}

          {stage === "ready" && (
            <section className="panel ready-panel" aria-live="polite">
              <div className="ready-heading">
                <span className="ready-check"><Icon name="check" size={19} /></span>
                <div><h2>Requirements are clear</h2><p>The agent has enough context to start implementing.</p></div>
              </div>
              <div className="markdown-answer agent-analysis-summary">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{formatAnalysisMarkdown(analysis)}</ReactMarkdown>
              </div>
              <div className="form-footer">
                <span>Auto-commit is off. Your changes will not be committed.</span>
                <button className="button button-primary" type="button" onClick={runWorkflow} disabled={busy}>
                  {busy ? <><span className="spinner" /> Starting…</> : <>Run implementation <Icon name="arrow" size={16} /></>}
                </button>
              </div>
            </section>
          )}

          {stage === "running" && (
            <section className="panel running-panel" aria-live="polite">
              <span className="running-orbit"><Icon name="spark" size={23} /></span>
              <div><h2>Your agent is at work</h2><p>Planning, implementing, building, testing, and reviewing your changes. This may take a few minutes.</p></div>
              <div className="running-track"><span /></div>
              <div className="running-caption">WORKFLOW IN PROGRESS <span>·</span> Keep this page open</div>
            </section>
          )}

          {stage === "finished" && workflow && (
            <section className="panel result-panel" aria-live="polite">
              <div className="ready-heading">
                <span className={`ready-check ${workflowFailed ? "failed-check" : ""}`}>{workflowFailed ? "!" : <Icon name="check" size={19} />}</span>
                <div><h2>{workflowFailed ? "Workflow needs attention" : "Workflow finished"}</h2><p>Review the result returned by your agent.</p></div>
                <span className={`result-state ${workflowFailed ? "failed-state" : ""}`}>{workflowState}</span>
              </div>
              <div className="markdown-answer agent-result">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{formatWorkflowMarkdown(workflow)}</ReactMarkdown>
              </div>
              <div className="form-footer">
                <span>Auto-commit was disabled for this run.</span>
                <button className="button button-secondary" type="button" onClick={() => { setStage("setup"); setWorkflow(null); }}>
                  <Icon name="refresh" size={16} /> New task
                </button>
              </div>
            </section>
          )}
          </>}

          <footer className="app-footer">
            <span><span className="footer-dot" /> PRIVATE BY DESIGN</span>
            <span>Powered by your AI Coding Agent API</span>
          </footer>
        </div>
      </section>
    </main>
  );
}

export default App;
