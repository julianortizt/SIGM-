import { createRoot } from "react-dom/client";
import "./index.css";
import { RouterProvider, createBrowserRouter } from "react-router-dom";
import type { RouteObject } from "react-router-dom";
import ModuleManager from "./components/ModuleManager";

const routes: RouteObject[] = [
  {
    path: "/",
    element: <ModuleManager />,
  },
];

const router = createBrowserRouter(routes);

createRoot(document.getElementById("root")!).render(
  <RouterProvider router={router} />
);