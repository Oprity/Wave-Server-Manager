// AC Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { Route, Routes } from 'react-router-dom';
import Layout from './components/Layout';
import ProfilesPage from './pages/ProfilesPage';
import ProfileEditPage from './pages/ProfileEditPage';

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<ProfilesPage />} />
        <Route path="/profiles/new" element={<ProfileEditPage />} />
        <Route path="/profiles/:id" element={<ProfileEditPage />} />
      </Routes>
    </Layout>
  );
}
