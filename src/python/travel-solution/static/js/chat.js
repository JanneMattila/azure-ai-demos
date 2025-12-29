document.addEventListener('DOMContentLoaded', () => {
  const chatId = window.chatId;
  const messagesEl = document.getElementById('messages');
  const form = document.getElementById('chatForm');
  const textarea = document.getElementById('message');
  let typingNode = document.getElementById('typing');

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

  async function submitMessage() {
    const content = textarea.value.trim();
    if (!content) return;
    appendMessage(content, 'user');
    textarea.value = '';

    showTyping();

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
      const assistantBubble = createAssistantBubble();
      let firstChunk = true;

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        if (firstChunk) {
          hideTyping();
          firstChunk = false;
        }
        assistantBubble.innerHTML = marked.parse(buffer);
        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
      }

      // Flush any remaining decoded text
      const trailing = decoder.decode();
      if (trailing) {
        buffer += trailing;
        assistantBubble.innerHTML = marked.parse(buffer);
      }
    } catch (err) {
      appendMessage('Network error. Please try again.', 'assistant');
      console.error(err);
    } finally {
      hideTyping();
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
