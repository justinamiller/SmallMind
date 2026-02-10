# SmallMind Showcase - Testing Guide

## Quick Test

To verify the application works:

1. **Build and Run:**
   ```bash
   cd SmallMind.Showcase/src/SmallMind.Showcase.Web
   dotnet build
   dotnet run
   ```

2. **Open Browser:**
   - Navigate to `http://localhost:5127` (or the port shown in console)
   - You should see the SmallMind Showcase interface

3. **Expected Behavior (No Models):**
   - Purple gradient header with "SmallMind Showcase" title
   - "No model loaded" warning badge
   - Welcome message: "Select a model, then create or select a session to start chatting"
   - Warning: "No models found in the models directory"
   - Three-column layout: sessions sidebar | main area | metrics panel
   - "+ New" button is disabled (no model loaded)

## Testing With a Model

1. **Add a model file:**
   ```bash
   # Create models directory
   mkdir -p SmallMind.Showcase/src/SmallMind.Showcase.Web/models
   
   # Copy a .gguf or .smq model file into it
   # Example: smollm2-135m-instruct-q4_0.gguf
   ```

2. **Restart the application**

3. **Expected Behavior (With Models):**
   - Model card(s) appear in the "Available Models" section
   - Click "Load Model" to load a model
   - Once loaded:
     - Header shows "Model: <name> (<quantization>)" in green badge
     - "+ New" button becomes enabled
     - Click "+ New" to create a chat session
     - Type a message and press Enter to send
     - Streaming response appears token-by-token
     - Metrics update in real-time (TTFT, tok/s, latency, etc.)

## Testing Checklist

### UI Rendering ✅
- [x] Three-column layout renders correctly
- [x] Purple gradient header displays
- [x] Sessions sidebar on the left
- [x] Main chat area in the center
- [x] Metrics panel on the right
- [x] Bootstrap styling applied

### Model Management
- [ ] Models are discovered from the models directory
- [ ] Model cards display name, size, architecture, quantization
- [ ] "Load Model" button loads the model
- [ ] Active model shown in header badge
- [ ] Model loading errors are displayed clearly

### Session Management
- [ ] "+ New" button creates a new session
- [ ] Sessions appear in the left sidebar
- [ ] Clicking a session switches to it
- [ ] Sessions persist across app restarts (stored in .data/)

### Chat Functionality
- [ ] User can type a message in the textarea
- [ ] Enter key sends the message
- [ ] Shift+Enter adds a newline
- [ ] User message appears in the chat
- [ ] Streaming response appears token-by-token
- [ ] "Stop" button cancels generation
- [ ] Error messages display in dismissible alerts

### Metrics Display
- [ ] TTFT displays after first token
- [ ] Prefill tok/s calculates correctly
- [ ] Decode tok/s updates during generation
- [ ] Token counts update (prompt, generated, total)
- [ ] Memory metrics show heap size and GC counts
- [ ] Percentiles display after multiple requests

### Performance
- [ ] No JavaScript errors in browser console
- [ ] UI remains responsive during generation
- [ ] Metrics update approximately every 500ms
- [ ] Memory usage is reasonable
- [ ] Session data persists correctly

## Known Limitations (By Design)

1. **No Markdown Rendering**: Responses display as plain text (future enhancement)
2. **No Session Rename/Delete**: Only create and switch (future enhancement)
3. **No Regenerate/Clear**: Basic chat only (future enhancement)
4. **Simple Prompt Formatting**: Uses basic "User: / Assistant:" format (not chat templates)
5. **Single Active Generation**: Cannot run multiple generations concurrently

## Troubleshooting

### Build Errors
- Ensure .NET 10 SDK is installed
- Run `dotnet restore` before building
- Check that all project references are correct

### Runtime Errors
- Check browser console for JavaScript errors
- Check application logs for server-side errors
- Verify models directory exists and has correct permissions
- Ensure model files are not corrupted

### Performance Issues
- CPU-only inference is inherently slow
- Smaller models (135M params) will be faster
- Quantized models (Q4) are faster than F32
- Expect 5-15 tok/s on typical CPU

## Success Criteria

The application is working correctly if:

1. ✅ It builds without errors
2. ✅ It runs and serves the UI on localhost
3. ✅ The UI renders with the three-column layout
4. ✅ Models can be discovered and loaded (when present)
5. ✅ Chat sessions can be created and messages sent
6. ✅ Streaming responses work with token-by-token updates
7. ✅ Metrics display and update in real-time
8. ✅ Sessions persist to JSON files
9. ✅ No third-party dependencies added to SmallMind
10. ✅ Clean separation between Core services and Web UI
