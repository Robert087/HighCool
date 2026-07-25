import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, HashRouter } from "react-router-dom";
import App from "./app/App";
import { isDesktopRuntime } from "./services/apiRuntime";
import "./design/tokens.css";
import "./styles/ui.css";
import "./styles/global.css";

const Router = isDesktopRuntime() ? HashRouter : BrowserRouter;

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Router>
      <App />
    </Router>
  </React.StrictMode>,
);
