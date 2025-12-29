document.addEventListener('DOMContentLoaded', () => {
  const chatId = window.chatId;
  const messagesEl = document.getElementById('messages');
  const form = document.getElementById('chatForm');
  const textarea = document.getElementById('message');
  const button = form?.querySelector('button');
  let typingNode = document.getElementById('typing');
  let isStreaming = false;
  let cancelRequested = false;

  function createAssistantBubble() {
    const bubble = document.createElement('div');
    bubble.className = 'bubble assistant';
    messagesEl.appendChild(bubble);
    return bubble;
  }

  function showTyping() {
    if (!messagesEl) return;
    if (!typingNode) {
      const row = document.createElement('div');
      row.className = 'typing-row';
      const bubble = document.createElement('div');
      bubble.className = 'bubble assistant typing';
      for (let i = 0; i < 3; i++) {
        const dot = document.createElement('span');
        dot.className = 'typing-dot';
        bubble.appendChild(dot);
      }
      row.appendChild(bubble);
      typingNode = row;
    }
    messagesEl.appendChild(typingNode);
    typingNode.style.display = 'flex';
    window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
  }

  function hideTyping() {
    if (typingNode) typingNode.style.display = 'none';
  }

  function appendMessage(text, role) {
    const bubble = document.createElement('div');
    bubble.className = 'bubble ' + role;
    const html = role === 'assistant' ? marked.parse(text) : text.replace(/</g, '&lt;');
    bubble.innerHTML = html;
    messagesEl.appendChild(bubble);
    window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
  }

  function setButtonState(state) {
    if (!button) return;
    if (state === 'running') {
      button.textContent = '■';
      button.classList.add('stop');
    } else {
      button.textContent = '▶';
      button.classList.remove('stop');
    }
  }

  async function requestCancel() {
    if (!isStreaming) return;
    cancelRequested = true;
    try {
      await fetch(`/api/chats/${chatId}/cancel`, { method: 'POST' });
    } catch (err) {
      console.error('Cancel failed', err);
    }
  }

  async function submitMessage() {
    if (isStreaming) {
      await requestCancel();
      return;
    }

    const content = textarea.value.trim();
    if (!content) return;
    appendMessage(content, 'user');
    textarea.value = '';

    showTyping();
    isStreaming = true;
    cancelRequested = false;
    setButtonState('running');

    try {
      const res = await fetch(`/api/chats/${chatId}/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: content })
      });

      if (!res.ok || !res.body) {
        appendMessage('Sorry, something went wrong.', 'assistant');
        return;
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let assistantBubble = null;
      let firstChunk = true;

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        const chunkText = decoder.decode(value, { stream: true });
        if (!chunkText.trim()) continue;

        buffer += chunkText;
        if (firstChunk) {
          hideTyping();
          assistantBubble = createAssistantBubble();
          firstChunk = false;
        }
        if (assistantBubble) {
          assistantBubble.innerHTML = marked.parse(buffer);
          window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
        }
      }

      // Flush any remaining decoded text
      const trailing = decoder.decode();
      if (trailing.trim()) {
        buffer += trailing;
        if (!assistantBubble) {
          hideTyping();
          assistantBubble = createAssistantBubble();
        }
        assistantBubble.innerHTML = marked.parse(buffer);
      }

      if (cancelRequested) {
        if (!assistantBubble) {
          hideTyping();
          assistantBubble = createAssistantBubble();
        }
        assistantBubble.innerHTML = marked.parse('User cancelled');
      }
    } catch (err) {
      if (!cancelRequested) {
        appendMessage('Network error. Please try again.', 'assistant');
      }
      console.error(err);
    } finally {
      hideTyping();
      isStreaming = false;
      setButtonState('idle');
      cancelRequested = false;
    }
  }

  if (form) {
    form.addEventListener('submit', async (e) => {
      e.preventDefault();
      await submitMessage();
    });

    // Allow Enter to send; Shift+Enter inserts newline
    textarea.addEventListener('keydown', async (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        await submitMessage();
      }
    });
  }
});
