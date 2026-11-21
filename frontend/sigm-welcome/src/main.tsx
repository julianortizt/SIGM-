import { createRoot } from "react-dom/client";
import "./index.css";
import { RouterProvider, createBrowserRouter } from "react-router-dom";
import type { RouteObject } from "react-router-dom";
import LandingPage from "./components/LandingPage";

const routes: RouteObject[] = [
  {
    path: "/",
    element: <LandingPage />,
  },
];

const router = createBrowserRouter(routes, { basename: import.meta.env.BASE_URL });

createRoot(document.getElementById("root")!).render(
  <RouterProvider router={router} />
);
